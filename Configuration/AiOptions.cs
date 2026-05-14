namespace StudentManagementSystem.Configuration;

public class AiOptions
{
    public const string SectionName = "AI";

    public int MaxUserMessageLength { get; set; } = 4000;

    public int MaxConversationMessagesPersisted { get; set; } = 80;

    /// <summary>Marks below (ObtainedMarks / TotalMarks) this ratio are treated as failing / at-risk.</summary>
    public double WeakMarksRatioThreshold { get; set; } = 0.4;
}
