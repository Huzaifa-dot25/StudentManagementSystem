using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentManagementSystem.Configuration;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models.AI.Dtos;

namespace StudentManagementSystem.Services.AI;

public interface IAiDashboardComposer
{
    Task<AiDashboardDto> BuildAsync(AiSecurityContext security, CancellationToken cancellationToken);
}

/// <summary>Aggregates EF metrics into dashboard cards and suggested notifications (no raw SQL).</summary>
public sealed class AiDashboardComposer : IAiDashboardComposer
{
    private readonly ApplicationDbContext _db;
    private readonly AiOptions _ai;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<AiDashboardComposer> _logger;

    public AiDashboardComposer(ApplicationDbContext db, IOptions<AiOptions> ai, IGeminiClient gemini, ILogger<AiDashboardComposer> logger)
    {
        _db = db;
        _ai = ai.Value;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<AiDashboardDto> BuildAsync(AiSecurityContext security, CancellationToken cancellationToken)
    {
        var cards = new List<AiInsightCardDto>();
        var notifications = new List<AiNotificationSuggestionDto>();

        if (security.IsAdmin || security.CanViewFees)
        {
            var unpaid = await _db.FeeChallans.CountAsync(f => f.Status == "Unpaid", cancellationToken);
            var unpaidAmt = await _db.FeeChallans.Where(f => f.Status == "Unpaid").SumAsync(f => (decimal?)(f.Amount + f.Arrears), cancellationToken) ?? 0;
            cards.Add(new AiInsightCardDto { Title = "Unpaid challans", Value = unpaid.ToString(), Subtitle = unpaidAmt.ToString("C"), Tone = unpaid > 0 ? "warning" : "success" });
            if (unpaid > 0)
            {
                notifications.Add(new AiNotificationSuggestionDto
                {
                    Category = "Fees",
                    Title = "Outstanding fee challans",
                    Body = $"{unpaid} unpaid challans totaling {unpaidAmt:C}. Consider reminders for parents.",
                    Severity = "warning"
                });
            }
        }

        if (security.IsAdmin || security.CanViewResults)
        {
            var threshold = (decimal)_ai.WeakMarksRatioThreshold;
            var weakCount = await _db.StudentResults.AsNoTracking()
                .Where(r => r.TotalMarks > 0 && r.ObtainedMarks / r.TotalMarks < threshold)
                .Select(r => r.StudentID).Distinct().CountAsync(cancellationToken);
            cards.Add(new AiInsightCardDto { Title = "Students with weak marks", Value = weakCount.ToString(), Subtitle = $"Below {threshold:P0} of obtained/total", Tone = weakCount > 0 ? "danger" : "neutral" });
            if (weakCount > 0)
            {
                notifications.Add(new AiNotificationSuggestionDto
                {
                    Category = "Academic",
                    Title = "Academic risk",
                    Body = $"{weakCount} students have result rows under the configured weak threshold.",
                    Severity = "danger"
                });
            }

            var worstClass = await _db.StudentResults.AsNoTracking()
                .Where(r => r.TotalMarks > 0)
                .GroupBy(r => r.Class)
                .Select(g => new { Class = g.Key, Avg = g.Average(x => x.ObtainedMarks / x.TotalMarks) })
                .OrderBy(x => x.Avg)
                .FirstOrDefaultAsync(cancellationToken);
            if (worstClass != null)
            {
                cards.Add(new AiInsightCardDto
                {
                    Title = "Lowest average class (marks ratio)",
                    Value = worstClass.Class ?? "—",
                    Subtitle = $"{worstClass.Avg:P1} average obtained/total",
                    Tone = "info"
                });
            }
        }

        if (security.IsAdmin || security.CanViewStudents)
        {
            var joinedYtd = await _db.Students.CountAsync(s => s.RegistrationDate.Year == DateTime.UtcNow.Year, cancellationToken);
            cards.Add(new AiInsightCardDto { Title = "New admissions (calendar year)", Value = joinedYtd.ToString(), Subtitle = DateTime.UtcNow.Year.ToString(), Tone = "neutral" });
        }

        string? narrative = null;
        try
        {
            var summaryPrompt = $"""
Summarize in 2 short paragraphs for a school admin dashboard. Cards: {string.Join("; ", cards.Select(c => c.Title + "=" + c.Value))}.
Notifications planned: {notifications.Count}. Do not invent numbers beyond these facts.
""";
            narrative = await _gemini.GenerateContentAsync(new List<(string, string)>
            {
                ("system", "You are a concise school analytics narrator. Markdown allowed."),
                ("user", summaryPrompt)
            }, requireJsonObject: false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dashboard narrative skipped (Gemini unavailable).");
        }

        return new AiDashboardDto { Cards = cards, NarrativeSummary = narrative, SuggestedNotifications = notifications };
    }
}
