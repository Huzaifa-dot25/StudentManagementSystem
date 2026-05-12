using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Controllers
{
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

        public async Task<IActionResult> ManageResult(string session, string className, string section, string term, string examType)
        {
            ViewBag.Sessions = await _context.Admissions.Select(a => a.Session).Distinct().ToListAsync();
            ViewBag.Classes  = await _context.Admissions.Select(a => a.Class).Distinct().ToListAsync();
            ViewBag.Sections = await _context.Admissions.Select(a => a.Section).Distinct().ToListAsync();

            var students = new List<Student>();
            if (!string.IsNullOrEmpty(className))
            {
                students = await _context.Students
                    .Include(s => s.Admission)
                    .Where(s => s.Admission != null && s.Admission.Class == className &&
                                (string.IsNullOrEmpty(section) || s.Admission.Section == section))
                    .ToListAsync();
            }

            return View(students);
        }

        public async Task<IActionResult> ResultsGrid(string? session, string? className, string? section, string? term)
        {
            ViewBag.Sessions = await _context.StudentResults.Select(r => r.Session).Distinct().OrderByDescending(s => s).ToListAsync();
            ViewBag.Classes  = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Class)).Select(a => a.Class!).Distinct().OrderBy(c => c).ToListAsync();
            ViewBag.Sections = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Section)).Select(a => a.Section!).Distinct().OrderBy(s => s).ToListAsync();
            ViewBag.Terms    = await _context.StudentResults.Select(r => r.Term).Distinct().OrderBy(t => t).ToListAsync();

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
            ViewBag.Sessions = await _context.StudentResults.Select(r => r.Session).Distinct().OrderByDescending(s => s).ToListAsync();
            ViewBag.Classes  = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Class)).Select(a => a.Class!).Distinct().OrderBy(c => c).ToListAsync();
            ViewBag.Sections = await _context.Admissions.Where(a => !string.IsNullOrEmpty(a.Section)).Select(a => a.Section!).Distinct().OrderBy(s => s).ToListAsync();
            ViewBag.Terms    = await _context.StudentResults.Select(r => r.Term).Distinct().OrderBy(t => t).ToListAsync();
            ViewBag.Type     = string.IsNullOrEmpty(type) ? "EndOfYear" : type;

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

        public IActionResult TeacherAssignment()      => View();
        public IActionResult ManageStatements()        => View();
        public IActionResult ViewStatementsByClass()   => View();

        [HttpPost]
        public async Task<IActionResult> SaveResults(List<StudentResult> results)
        {
            if (results != null && results.Any())
            {
                foreach (var result in results)
                {
                    var existing = await _context.StudentResults.FirstOrDefaultAsync(r =>
                        r.StudentID == result.StudentID &&
                        r.Session   == result.Session   &&
                        r.Class     == result.Class     &&
                        r.Section   == result.Section   &&
                        r.Term      == result.Term      &&
                        r.ExamType  == result.ExamType  &&
                        r.Subject   == result.Subject);

                    if (existing != null)
                    {
                        existing.ObtainedMarks   = result.ObtainedMarks;
                        existing.Status          = result.Status;
                        existing.ExamDate        = result.ExamDate;
                        existing.DeclarationDate = result.DeclarationDate;
                        existing.TotalMarks      = result.TotalMarks;
                        existing.IsAnnounced     = result.IsAnnounced;
                    }
                    else
                    {
                        _context.StudentResults.Add(result);
                    }
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Results saved successfully!";
            }
            return RedirectToAction(nameof(ManageResult));
        }
    }
}
