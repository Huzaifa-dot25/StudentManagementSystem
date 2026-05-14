namespace StudentManagementSystem.Services.AI;

public sealed record TeacherScope(string Class, string? Section, string? Subject);

/// <summary>Resolved permissions and scope for AI data access (never passed to raw SQL).</summary>
public sealed class AiSecurityContext
{
    public required string UserId { get; init; }
    public required string? UserName { get; init; }
    public required string? Email { get; init; }
    public bool IsAdmin { get; init; }
    public bool CanViewStudents { get; init; }
    public bool CanViewFees { get; init; }
    public bool CanViewResults { get; init; }
    public int? LinkedStudentId { get; init; }
    public IReadOnlyList<TeacherScope> TeacherScopes { get; init; } = Array.Empty<TeacherScope>();

    public bool IsTeacherScoped => !IsAdmin && TeacherScopes.Count > 0;

    /// <summary>Student linked to account without staff student-directory permission.</summary>
    public bool IsStudentSelfOnly => LinkedStudentId.HasValue && !IsAdmin && !CanViewStudents;
}
