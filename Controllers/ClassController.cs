using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Controllers
{
    [Authorize(Policy = "CanViewStudents")]
    public class ClassController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(ViewByClass));
        }

        public async Task<IActionResult> ViewByClass()
        {
            var classes = await _context.Admissions
                .Where(a => !string.IsNullOrEmpty(a.Class))
                .GroupBy(a => a.Class)
                .Select(g => new ClassSummaryViewModel
                {
                    ClassName = g.Key!,
                    StudentCount = g.Count(),
                    MaleCount = g.Count(a => a.Student!.Gender == "Male"),
                    FemaleCount = g.Count(a => a.Student!.Gender == "Female")
                })
                .OrderBy(c => c.ClassName)
                .ToListAsync();

            if (!classes.Any())
            {
                // Fallback for empty DB
                var defaultClasses = new List<string> { "Class 1", "Class 2", "Class 3", "Class 4", "Class 5", "Class 6", "Class 7", "Class 8", "Class 9", "Class 10" };
                var list = defaultClasses.Select(c => new ClassSummaryViewModel { ClassName = c, StudentCount = 0 }).ToList();
                return View(list);
            }

            return View(classes);
        }

        [Authorize(Policy = "CanManageStudents")]
        public async Task<IActionResult> AssignStudents(string className, string section)
        {
            ViewBag.Classes = await _context.Admissions
                .Where(a => !string.IsNullOrEmpty(a.Class))
                .Select(a => a.Class)
                .Distinct()
                .ToListAsync();

            if (ViewBag.Classes.Count == 0)
            {
                ViewBag.Classes = new List<string> { "Class 1", "Class 2", "Class 3", "Class 4", "Class 5", "Class 6", "Class 7", "Class 8", "Class 9", "Class 10" };
            }

            ViewBag.Sections = await _context.Admissions
                .Where(a => !string.IsNullOrEmpty(a.Section))
                .Select(a => a.Section)
                .Distinct()
                .ToListAsync();

            if (ViewBag.Sections.Count == 0)
            {
                ViewBag.Sections = new List<string> { "A", "B", "C", "D" };
            }

            var students = _context.Students
                .Include(s => s.Admission)
                .AsQueryable();

            if (!string.IsNullOrEmpty(className))
            {
                students = students.Where(s => s.Admission != null && s.Admission.Class == className);
            }

            if (!string.IsNullOrEmpty(section))
            {
                students = students.Where(s => s.Admission != null && s.Admission.Section == section);
            }

            return View(await students.ToListAsync());
        }

        [HttpPost]
        [Authorize(Policy = "CanManageStudents")]
        public async Task<IActionResult> BulkAssign(int[] studentIds, string targetClass, string targetSection, string targetSession)
        {
            if (studentIds == null || studentIds.Length == 0)
            {
                TempData["Error"] = "No students selected.";
                return RedirectToAction(nameof(AssignStudents));
            }

            var admissions = await _context.Admissions
                .Where(a => studentIds.Contains(a.StudentID))
                .ToListAsync();

            foreach (var admission in admissions)
            {
                admission.Class = targetClass;
                admission.Section = targetSection;
                if (!string.IsNullOrEmpty(targetSession))
                {
                    admission.Session = targetSession;
                }
            }

            // For students who don't have an admission record yet
            var existingStudentIds = admissions.Select(a => a.StudentID).ToList();
            var missingStudentIds = studentIds.Except(existingStudentIds).ToList();

            foreach (var sId in missingStudentIds)
            {
                _context.Admissions.Add(new Admission
                {
                    StudentID = sId,
                    Class = targetClass,
                    Section = targetSection,
                    Session = targetSession ?? "2024-2025"
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Successfully assigned {studentIds.Length} students to {targetClass} ({targetSection}).";
            
            return RedirectToAction(nameof(AssignStudents), new { className = targetClass, section = targetSection });
        }
    }
}
