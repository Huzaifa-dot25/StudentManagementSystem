using System.Text.Json.Serialization;

namespace StudentManagementSystem.Models.AI;

/// <summary>Structured output from the intent interpreter (never SQL).</summary>
public class AiInterpretedIntent
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = AiIntents.GeneralAnswer;

    [JsonPropertyName("classFilter")]
    public string? ClassFilter { get; set; }

    [JsonPropertyName("sectionFilter")]
    public string? SectionFilter { get; set; }

    [JsonPropertyName("subjectFilter")]
    public string? SubjectFilter { get; set; }

    [JsonPropertyName("topN")]
    public int? TopN { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("studentNameOrRoll")]
    public string? StudentNameOrRoll { get; set; }

    [JsonPropertyName("sessionFilter")]
    public string? SessionFilter { get; set; }
}

public static class AiIntents
{
    public const string GeneralAnswer = "general_answer";
    public const string FeeDefaulters = "fee_defaulters";
    public const string UnpaidFeeSummary = "unpaid_fee_summary";
    public const string WeakStudentsResults = "weak_students_results";
    public const string TopStudentsSubject = "top_students_subject";
    public const string ClassAverageMarks = "class_average_marks";
    public const string StudentsJoinedYear = "students_joined_year";
    public const string StudentLookup = "student_lookup";
    public const string TeacherAssignments = "teacher_assignments";
    public const string AttendanceCapability = "attendance_capability";
    public const string ExamPresenceSummary = "exam_presence_summary";
    public const string RiskSummary = "risk_summary";
}
