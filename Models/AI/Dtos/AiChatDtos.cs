namespace StudentManagementSystem.Models.AI.Dtos;

public class AiChatRequestDto
{
    public Guid? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AiChatResponseDto
{
    public Guid ConversationId { get; set; }
    public string Reply { get; set; } = string.Empty;
    public string? IntentUsed { get; set; }
    public bool UsedDatabase { get; set; }
}

public class AiConversationSummaryDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class AiDashboardDto
{
    public IReadOnlyList<AiInsightCardDto> Cards { get; set; } = Array.Empty<AiInsightCardDto>();
    public string? NarrativeSummary { get; set; }
    public IReadOnlyList<AiNotificationSuggestionDto> SuggestedNotifications { get; set; } = Array.Empty<AiNotificationSuggestionDto>();
}

public class AiInsightCardDto
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Tone { get; set; } = "neutral";
}

public class AiNotificationSuggestionDto
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
}

public class AiReportRequestDto
{
    public string ReportType { get; set; } = "fee_defaulters";
    public string? ClassFilter { get; set; }
    public string Format { get; set; } = "xlsx";
}
