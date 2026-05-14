using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StudentManagementSystem.Data;

namespace StudentManagementSystem.Services.AI;

public interface IAiReportGenerationService
{
    Task<byte[]> FeeDefaultersExcelAsync(string? classFilter, AiSecurityContext security, CancellationToken cancellationToken);
    Task<byte[]> ExecutiveSummaryPdfAsync(AiSecurityContext security, CancellationToken cancellationToken);
}

public sealed class AiReportGenerationService : IAiReportGenerationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AiReportGenerationService> _logger;

    static AiReportGenerationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public AiReportGenerationService(ApplicationDbContext db, ILogger<AiReportGenerationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<byte[]> FeeDefaultersExcelAsync(string? classFilter, AiSecurityContext security, CancellationToken cancellationToken)
    {
        if (!security.IsAdmin && !security.CanViewFees)
            throw new UnauthorizedAccessException();

        var q = _db.FeeChallans.AsNoTracking()
            .Include(f => f.Student).ThenInclude(s => s!.Admission)
            .Where(f => f.Status == "Unpaid");

        if (!string.IsNullOrWhiteSpace(classFilter))
            q = q.Where(f => f.ClassName == classFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(f => f.Student != null && f.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == f.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == f.Student.Admission.Section)));
        }

        var rows = await q.OrderBy(f => f.DueDate).Take(5000)
            .Select(f => new { f.ChallanID, f.StudentID, Name = f.Student!.Name, f.ClassName, f.Amount, f.Arrears, f.DueDate, f.Status })
            .ToListAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Fee defaulters");
        ws.Cell(1, 1).Value = "ChallanID";
        ws.Cell(1, 2).Value = "StudentID";
        ws.Cell(1, 3).Value = "Name";
        ws.Cell(1, 4).Value = "Class";
        ws.Cell(1, 5).Value = "Amount";
        ws.Cell(1, 6).Value = "Arrears";
        ws.Cell(1, 7).Value = "DueDate";
        ws.Cell(1, 8).Value = "Status";
        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.ChallanID;
            ws.Cell(r, 2).Value = x.StudentID;
            ws.Cell(r, 3).Value = x.Name;
            ws.Cell(r, 4).Value = x.ClassName;
            ws.Cell(r, 5).Value = x.Amount;
            ws.Cell(r, 6).Value = x.Arrears;
            ws.Cell(r, 7).Value = x.DueDate;
            ws.Cell(r, 8).Value = x.Status;
            r++;
        }
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExecutiveSummaryPdfAsync(AiSecurityContext security, CancellationToken cancellationToken)
    {
        if (!security.IsAdmin && !security.CanViewStudents && !security.CanViewFees && !security.CanViewResults)
            throw new UnauthorizedAccessException();

        var totalStudents = await _db.Students.CountAsync(cancellationToken);
        var unpaid = await _db.FeeChallans.CountAsync(f => f.Status == "Unpaid", cancellationToken);
        var paidRev = await _db.FeeChallans.Where(f => f.Status == "Paid").SumAsync(f => (decimal?)f.Amount, cancellationToken) ?? 0;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text("SMS — AI executive summary").SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);
                page.Content().Column(c =>
                {
                    c.Item().Text($"Generated (UTC): {DateTime.UtcNow:u}");
                    c.Item().PaddingTop(10).Text($"Total students: {totalStudents}");
                    c.Item().Text($"Unpaid challans: {unpaid}");
                    c.Item().Text($"Recorded paid fee revenue (challan amounts): {paidRev:C}");
                    c.Item().PaddingTop(12).Text("This PDF is generated from controlled aggregates; it is not raw database output.").Italic().FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }
}
