using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Controllers
{
    [Authorize(Policy = "CanViewResults")]
    public class PrimaryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PrimaryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(ManageResult));
        }

        private async Task PopulateResultDropdowns()
        {
            var admSessions = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Session)).Select(a => a.Session!).Distinct().ToListAsync();
            var resSessions = await _context.StudentResults.Where(r => !string.IsNullOrEmpty(r.Session)).Select(r => r.Session!).Distinct().ToListAsync();
            
            var allSessions = admSessions.Union(resSessions).Distinct().OrderByDescending(s => s).ToList();
            if (!allSessions.Any())
            {
                allSessions = new List<string> { "2024 - 2025", "2025 - 2026" };
            }
            ViewBag.Sessions = allSessions;

            ViewBag.Classes  = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Class)).Select(a => a.Class!).Distinct().OrderBy(c => c).ToListAsync();
            ViewBag.Sections = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Section)).Select(a => a.Section!).Distinct().OrderBy(s => s).ToListAsync();

            var terms = await _context.StudentResults.Where(r => !string.IsNullOrEmpty(r.Term)).Select(r => r.Term).Distinct().OrderBy(t => t).ToListAsync();
            if (!terms.Any())
            {
                terms = new List<string> { "Term 1", "Term 2", "Annual" };
            }
            ViewBag.Terms = terms;

            ViewBag.ExamTypes = new List<string> { "Written", "Midterm", "Final", "Practical", "Oral" };
        }

        private static readonly string[] PrimarySubjectOptions =
        {
            "General Science", "Social Studies", "Mathematics", "English Language", "Urdu", "Islamiyat"
        };

        private async Task<ManageResultsPageViewModel> BuildManageResultsPageAsync(
            string? session, string? className, string? section, string? term, string? examType, string? subject)
        {
            var sessionEffective = session?.Trim() ?? "";
            var termEffective = string.IsNullOrWhiteSpace(term) ? "Term 1" : term.Trim();
            var examEffective = string.IsNullOrWhiteSpace(examType) ? "Written" : examType.Trim();
            var subjectTrim = subject?.Trim() ?? "";
            var subjectEffective = PrimarySubjectOptions.FirstOrDefault(s => s.Equals(subjectTrim, StringComparison.OrdinalIgnoreCase))
                ?? PrimarySubjectOptions[0];

            if (string.IsNullOrEmpty(className))
            {
                return new ManageResultsPageViewModel
                {
                    Session = sessionEffective,
                    ClassName = "",
                    Section = section?.Trim() ?? "",
                    Term = termEffective,
                    ExamType = examEffective,
                    Subject = subjectEffective
                };
            }

            var students = await _context.Students
                .Include(s => s.Admission)
                .Include(s => s.Parent)
                .Where(s => s.Admission != null && s.Admission.Class == className &&
                            (string.IsNullOrEmpty(section) || s.Admission.Section == section) &&
                            (string.IsNullOrEmpty(sessionEffective) || s.Admission.Session == sessionEffective))
                .OrderBy(s => s.Admission!.RollNo)
                .ThenBy(s => s.Name)
                .ToListAsync();

            var studentIds = students.Select(s => s.StudentID).ToList();
            var existingRows = studentIds.Count == 0
                ? new List<StudentResult>()
                : await _context.StudentResults.AsNoTracking()
                    .Where(r => studentIds.Contains(r.StudentID) &&
                                r.Class == className &&
                                r.Term == termEffective &&
                                r.ExamType == examEffective &&
                                r.Subject == subjectEffective &&
                                (string.IsNullOrEmpty(sessionEffective) || r.Session == sessionEffective))
                    .ToListAsync();

            var results = new List<StudentResult>();
            foreach (var student in students)
            {
                var adm = student.Admission!;
                var sess = !string.IsNullOrEmpty(sessionEffective) ? sessionEffective : (adm.Session ?? "");
                var sec = adm.Section ?? "";

                var existing = existingRows.FirstOrDefault(r =>
                    r.StudentID == student.StudentID &&
                    r.Section == sec &&
                    r.Session == sess);

                if (existing != null)
                {
                    results.Add(new StudentResult
                    {
                        Id = existing.Id,
                        StudentID = student.StudentID,
                        Session = existing.Session,
                        Class = existing.Class,
                        Section = existing.Section,
                        Term = existing.Term,
                        ExamType = existing.ExamType,
                        Subject = existing.Subject,
                        SubSubject = existing.SubSubject,
                        ExamDate = existing.ExamDate,
                        DeclarationDate = existing.DeclarationDate,
                        TotalMarks = existing.TotalMarks,
                        ObtainedMarks = existing.ObtainedMarks,
                        Status = existing.Status,
                        IsAnnounced = existing.IsAnnounced,
                        Student = student
                    });
                }
                else
                {
                    results.Add(new StudentResult
                    {
                        Id = 0,
                        StudentID = student.StudentID,
                        Session = sess,
                        Class = adm.Class ?? "",
                        Section = sec,
                        Term = termEffective,
                        ExamType = examEffective,
                        Subject = subjectEffective,
                        SubSubject = "",
                        ExamDate = DateTime.Today,
                        DeclarationDate = DateTime.Today,
                        TotalMarks = 100,
                        ObtainedMarks = 0,
                        Status = "Present",
                        IsAnnounced = false,
                        Student = student
                    });
                }
            }

            return new ManageResultsPageViewModel
            {
                Session = sessionEffective,
                ClassName = className,
                Section = section?.Trim() ?? "",
                Term = termEffective,
                ExamType = examEffective,
                Subject = subjectEffective,
                Results = results
            };
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageResult(string? session, string? className, string? section, string? term, string? examType, string? subject)
        {
            await PopulateResultDropdowns();
            ViewBag.Subjects = PrimarySubjectOptions;

            var page = await BuildManageResultsPageAsync(session, className, section, term, examType, subject);
            return View(page);
        }

        public async Task<IActionResult> ResultsGrid(string? session, string? className, string? section, string? term)
        {
            await PopulateResultDropdowns();

            var vm = new ResultsGridViewModel
            {
                SelectedSession = session,
                SelectedClass   = className,
                SelectedSection = section,
                SelectedTerm    = term
            };

            bool anyFilter = !string.IsNullOrEmpty(session) || !string.IsNullOrEmpty(className) ||
                             !string.IsNullOrEmpty(section) || !string.IsNullOrEmpty(term);

            if (anyFilter)
            {
                var results = await _context.StudentResults
                    .Include(r => r.Student).ThenInclude(s => s!.Admission)
                    .Include(r => r.Student).ThenInclude(s => s!.Parent)
                    .Where(r =>
                        (string.IsNullOrEmpty(session)   || r.Session == session)   &&
                        (string.IsNullOrEmpty(className) || r.Class   == className) &&
                        (string.IsNullOrEmpty(section)   || r.Section == section)   &&
                        (string.IsNullOrEmpty(term)      || r.Term    == term))
                    .ToListAsync();

                vm.ReportCards = results
                    .GroupBy(r => r.StudentID)
                    .Select(g =>
                    {
                        var first   = g.First();
                        var student = first.Student;
                        return new StudentReportCard
                        {
                            StudentID  = g.Key,
                            Name       = student?.Name ?? "Unknown",
                            RollNo     = student?.Admission?.RollNo,
                            Class      = first.Class,
                            Section    = first.Section,
                            Session    = first.Session,
                            Term       = first.Term,
                            FatherName = student?.Parent?.FatherName,
                            PhotoPath  = student?.PhotoPath,
                            Subjects   = g.OrderBy(r => r.Subject).ToList()
                        };
                    })
                    .OrderBy(c => c.RollNo)
                    .ToList();
            }

            return View(vm);
        }

        public async Task<IActionResult> PrintResult(string? session, string? className, string? section, string? term, string? type)
        {
            await PopulateResultDropdowns();
            ViewBag.Type = string.IsNullOrEmpty(type) ? "EndOfYear" : type;

            var results = await _context.StudentResults
                .Include(r => r.Student).ThenInclude(s => s!.Admission)
                .Include(r => r.Student).ThenInclude(s => s!.Parent)
                .Where(r =>
                    (string.IsNullOrEmpty(session)   || r.Session == session)   &&
                    (string.IsNullOrEmpty(className) || r.Class   == className) &&
                    (string.IsNullOrEmpty(section)   || r.Section == section)   &&
                    (string.IsNullOrEmpty(term)      || r.Term    == term))
                .ToListAsync();

            var reportCards = results
                .GroupBy(r => r.StudentID)
                .Select(g =>
                {
                    var first   = g.First();
                    var student = first.Student;
                    return new StudentReportCard
                    {
                        StudentID  = g.Key,
                        Name       = student?.Name ?? "Unknown",
                        RollNo     = student?.Admission?.RollNo,
                        Class      = first.Class,
                        Section    = first.Section,
                        Session    = first.Session,
                        Term       = first.Term,
                        FatherName = student?.Parent?.FatherName,
                        PhotoPath  = student?.PhotoPath,
                        Subjects   = g.OrderBy(r => r.Subject).ToList()
                    };
                })
                .OrderBy(c => c.RollNo)
                .ToList();

            return View(reportCards);
        }

        public async Task<IActionResult> TeacherAssignment()
        {
            var today = DateTime.Today;
            // Only show assignments where EndDate is today or in the future
            var assignments = await _context.TeacherAssignments
                .Where(a => a.EndDate >= today)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewBag.Classes = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Class)).Select(a => a.Class!).Distinct().ToListAsync();
            ViewBag.Sections = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Section)).Select(a => a.Section!).Distinct().ToListAsync();

            return View(assignments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanManageResults")]
        public async Task<IActionResult> SaveAssignment(TeacherAssignment model)
        {
            if (ModelState.IsValid)
            {
                if (model.Id == 0)
                {
                    _context.TeacherAssignments.Add(model);
                }
                else
                {
                    _context.TeacherAssignments.Update(model);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Assignment saved successfully!";
            }
            return RedirectToAction(nameof(TeacherAssignment));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanManageResults")]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var assignment = await _context.TeacherAssignments.FindAsync(id);
            if (assignment != null)
            {
                _context.TeacherAssignments.Remove(assignment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Assignment deleted successfully!";
            }
            return RedirectToAction(nameof(TeacherAssignment));
        }

        public IActionResult ManageStatements()        => View();
        public IActionResult ViewStatementsByClass()   => View();

        [Authorize(Policy = "CanManageResults")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveResults([FromForm, Bind(Prefix = "Results")] List<StudentResult> results)
        {
            if (results == null || !results.Any())
            {
                TempData["ErrorMessage"] = "Nothing to save. Load a class and enter marks, then try again.";
                return RedirectToAction(nameof(ManageResult));
            }

            foreach (var posted in results)
            {
                posted.Student = null;
                posted.SubSubject ??= string.Empty;

                if (posted.Id > 0)
                {
                    var entity = await _context.StudentResults.FindAsync(posted.Id);
                    if (entity != null)
                    {
                        entity.ObtainedMarks = posted.ObtainedMarks;
                        entity.Status = posted.Status;
                        entity.ExamDate = posted.ExamDate;
                        entity.DeclarationDate = posted.DeclarationDate;
                        entity.TotalMarks = posted.TotalMarks;
                        entity.IsAnnounced = posted.IsAnnounced;
                    }
                }
                else
                {
                    posted.Id = 0;
                    _context.StudentResults.Add(posted);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Results saved successfully!";

            var first = results[0];
            return RedirectToAction(nameof(ManageResult), new
            {
                session = first.Session,
                className = first.Class,
                section = first.Section,
                term = first.Term,
                examType = first.ExamType,
                subject = first.Subject
            });
        }
    }
}
