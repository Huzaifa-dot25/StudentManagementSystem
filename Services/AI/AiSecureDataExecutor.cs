using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentManagementSystem.Configuration;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;
using StudentManagementSystem.Models.AI;

namespace StudentManagementSystem.Services.AI;

public interface IAiSecureDataExecutor
{
    /// <summary>Runs a controlled EF query for the interpreted intent. Never executes raw SQL.</summary>
    Task<object?> ExecuteAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken cancellationToken);
}

public sealed class AiSecureDataExecutor : IAiSecureDataExecutor
{
    private readonly ApplicationDbContext _db;
    private readonly AiOptions _ai;
    private readonly ILogger<AiSecureDataExecutor> _logger;

    public AiSecureDataExecutor(ApplicationDbContext db, IOptions<AiOptions> ai, ILogger<AiSecureDataExecutor> logger)
    {
        _db = db;
        _ai = ai.Value;
        _logger = logger;
    }

    public async Task<object?> ExecuteAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken cancellationToken)
    {
        try
        {
            return intent.Intent switch
            {
                AiIntents.FeeDefaulters => await FeeDefaultersAsync(intent, security, cancellationToken),
                AiIntents.UnpaidFeeSummary => await UnpaidFeeSummaryAsync(intent, security, cancellationToken),
                AiIntents.WeakStudentsResults => await WeakStudentsAsync(intent, security, cancellationToken),
                AiIntents.TopStudentsSubject => await TopStudentsAsync(intent, security, cancellationToken),
                AiIntents.ClassAverageMarks => await ClassAveragesAsync(intent, security, cancellationToken),
                AiIntents.StudentsJoinedYear => await JoinedYearAsync(intent, security, cancellationToken),
                AiIntents.StudentLookup => await StudentLookupAsync(intent, security, cancellationToken),
                AiIntents.TeacherAssignments => await TeacherAssignmentsAsync(security, cancellationToken),
                AiIntents.AttendanceCapability => AttendanceCapabilityPayload(),
                AiIntents.ExamPresenceSummary => await ExamPresenceAsync(intent, security, cancellationToken),
                AiIntents.RiskSummary => await RiskSummaryAsync(intent, security, cancellationToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI secure executor failed for intent {Intent}", intent.Intent);
            return new { error = "Data access failed for this request. Try a narrower question or contact an administrator." };
        }
    }

    private IQueryable<Student> BaseStudents(AiSecurityContext security)
    {
        var q = _db.Students.AsNoTracking().Include(s => s.Admission);

        if (security.IsAdmin)
            return q;

        if (security.LinkedStudentId is { } selfId && !security.CanViewStudents)
            return q.Where(s => s.StudentID == selfId);

        if (!security.CanViewStudents)
            return q.Where(s => false);

        if (security.IsTeacherScoped)
        {
            return q.Where(s => s.Admission != null && security.TeacherScopes.Any(ts =>
                ts.Class == s.Admission.Class &&
                (string.IsNullOrEmpty(ts.Section) || ts.Section == s.Admission.Section)));
        }

        return q;
    }

    private bool CanFees(AiSecurityContext s) => s.IsAdmin || s.CanViewFees;
    private bool CanResults(AiSecurityContext s) => s.IsAdmin || s.CanViewResults;

    private async Task<object> FeeDefaultersAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!CanFees(security))
            return new { denied = "You do not have permission to view fee data." };

        var take = Math.Clamp(intent.TopN ?? 25, 1, 50);
        var q = _db.FeeChallans.AsNoTracking()
            .Include(f => f.Student).ThenInclude(s => s!.Admission)
            .Where(f => f.Status == "Unpaid");

        if (!string.IsNullOrWhiteSpace(intent.ClassFilter))
            q = q.Where(f => f.ClassName == intent.ClassFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(f => f.Student != null && f.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == f.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == f.Student.Admission.Section)));
        }

        if (security.LinkedStudentId is { } sid && !security.IsAdmin && !security.CanViewStudents)
            q = q.Where(f => f.StudentID == sid);

        var rows = await q.OrderByDescending(f => f.DueDate).Take(take)
            .Select(f => new
            {
                f.ChallanID,
                f.StudentID,
                StudentName = f.Student!.Name,
                Class = f.ClassName,
                f.Amount,
                f.Arrears,
                f.DueDate,
                RollNo = f.Student.Admission != null ? f.Student.Admission.RollNo : null
            }).ToListAsync(ct);

        return new { feeDefaulters = rows };
    }

    private async Task<object> UnpaidFeeSummaryAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!CanFees(security))
            return new { denied = "You do not have permission to view fee data." };

        var q = _db.FeeChallans.AsNoTracking().Where(f => f.Status == "Unpaid");
        if (!string.IsNullOrWhiteSpace(intent.ClassFilter))
            q = q.Where(f => f.ClassName == intent.ClassFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(f => f.Student != null && f.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == f.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == f.Student.Admission.Section)));
        }

        var totalAmount = await q.SumAsync(f => (decimal?)(f.Amount + f.Arrears), ct) ?? 0;
        var count = await q.CountAsync(ct);
        var byClass = await q.GroupBy(f => f.ClassName)
            .Select(g => new { Class = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount + x.Arrears) })
            .ToListAsync(ct);

        return new { unpaidChallanCount = count, unpaidTotalAmount = totalAmount, byClass };
    }

    private async Task<object> WeakStudentsAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!CanResults(security))
            return new { denied = "You do not have permission to view academic results." };

        var threshold = (decimal)_ai.WeakMarksRatioThreshold;
        var take = Math.Clamp(intent.TopN ?? 15, 1, 50);

        var q = _db.StudentResults.AsNoTracking()
            .Include(r => r.Student).ThenInclude(s => s!.Admission)
            .Where(r => r.TotalMarks > 0 && r.ObtainedMarks / r.TotalMarks < threshold);

        if (!string.IsNullOrWhiteSpace(intent.SubjectFilter))
            q = q.Where(r => r.Subject == intent.SubjectFilter);
        if (!string.IsNullOrWhiteSpace(intent.ClassFilter))
            q = q.Where(r => r.Class == intent.ClassFilter);
        if (!string.IsNullOrWhiteSpace(intent.SessionFilter))
            q = q.Where(r => r.Session == intent.SessionFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(r => r.Student != null && r.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == r.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == r.Student.Admission.Section) &&
                                 (string.IsNullOrEmpty(ts.Subject) || ts.Subject == r.Subject)));
        }

        if (security.LinkedStudentId is { } sid && !security.IsAdmin && !security.CanViewStudents)
            q = q.Where(r => r.StudentID == sid);

        var rows = await q
            .OrderBy(r => r.ObtainedMarks / r.TotalMarks)
            .Take(take)
            .Select(r => new
            {
                r.StudentID,
                Name = r.Student!.Name,
                r.Subject,
                r.Class,
                r.Section,
                r.ObtainedMarks,
                r.TotalMarks,
                Ratio = r.TotalMarks == 0 ? 0 : r.ObtainedMarks / r.TotalMarks
            })
            .ToListAsync(ct);

        return new { weakStudents = rows, thresholdRatio = threshold };
    }

    private async Task<object> TopStudentsAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!CanResults(security))
            return new { denied = "You do not have permission to view academic results." };

        var take = Math.Clamp(intent.TopN ?? 10, 1, 50);
        var subject = intent.SubjectFilter;
        if (string.IsNullOrWhiteSpace(subject))
            return new { error = "Subject filter is required for top students ranking." };

        var q = _db.StudentResults.AsNoTracking()
            .Where(r => r.TotalMarks > 0 && r.Subject == subject);

        if (!string.IsNullOrWhiteSpace(intent.ClassFilter))
            q = q.Where(r => r.Class == intent.ClassFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(r => r.Student != null && r.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == r.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == r.Student.Admission.Section) &&
                                 ts.Subject == r.Subject));
        }

        var grouped = await q
            .GroupBy(r => new { r.StudentID, Name = r.Student!.Name, r.Class, r.Section })
            .Select(g => new
            {
                g.Key.StudentID,
                g.Key.Name,
                g.Key.Class,
                g.Key.Section,
                AvgRatio = g.Average(x => x.ObtainedMarks / x.TotalMarks)
            })
            .OrderByDescending(x => x.AvgRatio)
            .Take(take)
            .ToListAsync(ct);

        return new { subject, topStudents = grouped };
    }

    private async Task<object> ClassAveragesAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!CanResults(security))
            return new { denied = "You do not have permission to view academic results." };

        var q = _db.StudentResults.AsNoTracking().Where(r => r.TotalMarks > 0);
        if (!string.IsNullOrWhiteSpace(intent.SubjectFilter))
            q = q.Where(r => r.Subject == intent.SubjectFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(r => r.Student != null && r.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == r.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == r.Student.Admission.Section) &&
                                 (string.IsNullOrEmpty(ts.Subject) || ts.Subject == r.Subject)));
        }

        var rows = await q.GroupBy(r => r.Class)
            .Select(g => new
            {
                Class = g.Key,
                AvgPercent = g.Average(x => x.ObtainedMarks / x.TotalMarks) * 100
            })
            .OrderBy(x => x.AvgPercent)
            .ToListAsync(ct);

        return new { classAveragesPercent = rows, subject = intent.SubjectFilter };
    }

    private async Task<object> JoinedYearAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!security.CanViewStudents && !security.IsAdmin)
            return new { denied = "You do not have permission to browse students." };

        var year = intent.Year ?? DateTime.UtcNow.Year;
        var take = Math.Clamp(intent.TopN ?? 30, 1, 50);

        var q = BaseStudents(security)
            .Where(s => s.RegistrationDate.Year == year);

        if (!string.IsNullOrWhiteSpace(intent.ClassFilter))
            q = q.Where(s => s.Admission != null && s.Admission.Class == intent.ClassFilter);

        var rows = await q.OrderByDescending(s => s.RegistrationDate).Take(take)
            .Select(s => new { s.StudentID, s.Name, s.RegistrationDate, Class = s.Admission!.Class, Section = s.Admission.Section })
            .ToListAsync(ct);

        return new { year, students = rows };
    }

    private async Task<object> StudentLookupAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!security.CanViewStudents && !security.IsAdmin && security.LinkedStudentId is null)
            return new { denied = "You do not have permission to search students." };

        var term = intent.StudentNameOrRoll ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            return new { error = "Provide a name or roll number to search." };

        var q = BaseStudents(security)
            .Where(s => s.Name.Contains(term) ||
                        (s.Admission != null && s.Admission.RollNo != null && s.Admission.RollNo.Contains(term)));

        var rows = await q.Take(20)
            .Select(s => new { s.StudentID, s.Name, Class = s.Admission!.Class, Section = s.Admission.Section, RollNo = s.Admission.RollNo })
            .ToListAsync(ct);

        return new { matches = rows };
    }

    private async Task<object> TeacherAssignmentsAsync(AiSecurityContext security, CancellationToken ct)
    {
        var q = _db.TeacherAssignments.AsNoTracking().Where(a => a.EndDate >= DateTime.Today);
        if (!security.IsAdmin)
        {
            var name = security.UserName ?? security.Email ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return new { denied = "Unable to resolve teacher identity for assignments." };

            var local = (security.Email ?? "").Split('@')[0];
            q = q.Where(a => a.TeacherName.Contains(name) || (!string.IsNullOrEmpty(local) && a.TeacherName.Contains(local)));
        }

        var rows = await q.OrderByDescending(a => a.StartDate).Take(40).ToListAsync(ct);
        return new { assignments = rows.Select(a => new { a.Title, a.Class, a.Section, a.Subject, a.TeacherName, a.StartDate, a.EndDate }) };
    }

    private static object AttendanceCapabilityPayload() => new
    {
        note = "This SMS build does not store daily attendance records. Exam/assessment presence can be summarized from stored result rows (Present/Absent/Pending) where available.",
        suggestion = "Use intents around StudentResults or ask for fee defaulters / weak students."
    };

    private async Task<object> ExamPresenceAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!CanResults(security))
            return new { denied = "You do not have permission to view result-related presence." };

        var q = _db.StudentResults.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(intent.ClassFilter))
            q = q.Where(r => r.Class == intent.ClassFilter);
        if (!string.IsNullOrWhiteSpace(intent.SubjectFilter))
            q = q.Where(r => r.Subject == intent.SubjectFilter);

        if (security.IsTeacherScoped && !security.IsAdmin)
        {
            q = q.Where(r => r.Student != null && r.Student.Admission != null &&
                             security.TeacherScopes.Any(ts =>
                                 ts.Class == r.Student.Admission.Class &&
                                 (string.IsNullOrEmpty(ts.Section) || ts.Section == r.Student.Admission.Section)));
        }

        var grouped = await q.GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new { examRowStatusCounts = grouped, disclaimer = "Counts reflect stored examination/assessment status fields, not daily attendance." };
    }

    private async Task<object> RiskSummaryAsync(AiInterpretedIntent intent, AiSecurityContext security, CancellationToken ct)
    {
        if (!security.IsAdmin && !security.CanViewFees && !security.CanViewResults)
            return new { denied = "Insufficient permissions for risk summary." };

        var weak = !CanResults(security)
            ? 0
            : await _db.StudentResults.AsNoTracking()
                .Where(r => r.TotalMarks > 0 && r.ObtainedMarks / r.TotalMarks < (decimal)_ai.WeakMarksRatioThreshold)
                .Select(r => r.StudentID).Distinct().CountAsync(ct);

        var unpaidStudents = !CanFees(security)
            ? 0
            : await _db.FeeChallans.AsNoTracking()
                .Where(f => f.Status == "Unpaid")
                .Select(f => f.StudentID).Distinct().CountAsync(ct);

        return new
        {
            studentsWithWeakMarks = weak,
            studentsWithUnpaidChallans = unpaidStudents,
            note = "Risk is inferred from stored marks and unpaid challans only."
        };
    }
}
