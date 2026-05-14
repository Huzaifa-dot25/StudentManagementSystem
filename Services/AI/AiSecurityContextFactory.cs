using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;

namespace StudentManagementSystem.Services.AI;

public interface IAiSecurityContextFactory
{
    Task<AiSecurityContext> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class AiSecurityContextFactory : IAiSecurityContextFactory
{
    private readonly ApplicationDbContext _db;

    public AiSecurityContextFactory(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiSecurityContext> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var name = user.Identity?.Name;
        var email = user.FindFirstValue(ClaimTypes.Email);

        var isAdmin = user.IsInRole("Admin");
        var canStudents = isAdmin || user.HasClaim("Permission", "Students.View");
        var canFees = isAdmin || user.HasClaim("Permission", "Fees.View");
        var canResults = isAdmin || user.HasClaim("Permission", "Results.View");

        int? studentId = null;
        var sid = user.FindFirstValue("StudentId");
        if (int.TryParse(sid, out var parsed))
            studentId = parsed;

        var scopes = new List<TeacherScope>();
        if (!isAdmin && !string.IsNullOrWhiteSpace(name))
        {
            var assignments = await _db.TeacherAssignments.AsNoTracking()
                .Where(a => a.EndDate >= DateTime.Today && a.TeacherName.Contains(name))
                .Select(a => new { a.Class, a.Section, a.Subject })
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(email))
            {
                var local = email.Split('@')[0];
                var more = await _db.TeacherAssignments.AsNoTracking()
                    .Where(a => a.EndDate >= DateTime.Today &&
                                (a.TeacherName.Contains(email) || a.TeacherName.Contains(local)))
                    .Select(a => new { a.Class, a.Section, a.Subject })
                    .Distinct()
                    .ToListAsync(cancellationToken);
                assignments = assignments.Union(more).ToList();
            }

            foreach (var a in assignments)
                scopes.Add(new TeacherScope(a.Class, string.IsNullOrWhiteSpace(a.Section) ? null : a.Section, a.Subject));
        }

        return new AiSecurityContext
        {
            UserId = id,
            UserName = name,
            Email = email,
            IsAdmin = isAdmin,
            CanViewStudents = canStudents,
            CanViewFees = canFees,
            CanViewResults = canResults,
            LinkedStudentId = studentId,
            TeacherScopes = scopes
        };
    }
}
