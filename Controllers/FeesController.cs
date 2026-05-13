using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Controllers
{
    [Authorize(Policy = "CanViewFees")]
    public class FeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Fees/Index (Dashboard)
        public IActionResult Index()
        {
            return View();
        }

        // GET: Fees/Challans
        public async Task<IActionResult> Challans(string searchString)
        {
            var challans = _context.FeeChallans
                .Include(f => f.Student)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                challans = challans.Where(s => s.Student!.Name.Contains(searchString) 
                                            || s.StudentID.ToString() == searchString
                                            || s.ChallanID.Contains(searchString)
                                            || (s.Student!.Admission != null && s.Student.Admission.RollNo != null && s.Student.Admission.RollNo.Contains(searchString)));
            }

            return View(await challans.OrderByDescending(c => c.ChallanID).ToListAsync());
        }

        // GET: Fees/PrintChallan/ID
        public async Task<IActionResult> PrintChallan(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var challan = await _context.FeeChallans
                .Include(f => f.Student).ThenInclude(s => s!.Admission)
                .Include(f => f.Student).ThenInclude(s => s!.Parent)
                .FirstOrDefaultAsync(m => m.ChallanID == id);

            if (challan == null) return NotFound();

            return View(challan);
        }

        // GET: Fees/Generate
        public async Task<IActionResult> Generate()
        {
            ViewBag.Sessions = await _context.Admissions.Where(a => a.Session != null).Select(a => a.Session).Distinct().ToListAsync();
            ViewBag.Classes = await _context.Admissions.Where(a => a.Class != null).Select(a => a.Class).Distinct().ToListAsync();
            ViewBag.Sections = await _context.Admissions.Where(a => a.Section != null).Select(a => a.Section).Distinct().ToListAsync();
            
            return View(new GenerateChallanViewModel());
        }

        // POST: Fees/Generate
        [Authorize(Policy = "CanManageFees")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(GenerateChallanViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Find students matching criteria
                var students = await _context.Students
                    .Include(s => s.Admission)
                    .Where(s => s.Admission != null &&
                                s.Admission.Session == model.Session &&
                                s.Admission.Class == model.Class &&
                                s.Admission.Section == model.Section)
                    .ToListAsync();

                if (!students.Any())
                {
                    ModelState.AddModelError("", "No students found for the selected criteria.");
                    ViewBag.Sessions = await _context.Admissions.Where(a => a.Session != null).Select(a => a.Session).Distinct().ToListAsync();
                    ViewBag.Classes = await _context.Admissions.Where(a => a.Class != null).Select(a => a.Class).Distinct().ToListAsync();
                    ViewBag.Sections = await _context.Admissions.Where(a => a.Section != null).Select(a => a.Section).Distinct().ToListAsync();
                    return View(model);
                }

                int createdCount = 0;
                foreach (var student in students)
                {
                    // Basic fee calculation logic
                    decimal amount = 5000 * model.NumberOfMonths;

                    var challan = new FeeChallan
                    {
                        ChallanID = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                        StudentID = student.StudentID,
                        ClassName = student.Admission?.Class ?? "Unknown",
                        Month = model.StartMonth.ToString("MMM-yy"),
                        Amount = amount,
                        DueDate = model.DueDate,
                        Status = "Unpaid"
                    };

                    _context.FeeChallans.Add(challan);
                    createdCount++;

                    if (model.ExpirePreviousUnpaid)
                    {
                        var previousUnpaid = await _context.FeeChallans
                            .Where(c => c.StudentID == student.StudentID && c.Status == "Unpaid" && c.ChallanID != challan.ChallanID)
                            .ToListAsync();
                        
                        foreach (var prev in previousUnpaid)
                        {
                            prev.Status = "Expired";
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Successfully generated {createdCount} challans.";
                return RedirectToAction(nameof(Challans));
            }

            ViewBag.Sessions = await _context.Admissions.Where(a => a.Session != null).Select(a => a.Session).Distinct().ToListAsync();
            ViewBag.Classes = await _context.Admissions.Where(a => a.Class != null).Select(a => a.Class).Distinct().ToListAsync();
            ViewBag.Sections = await _context.Admissions.Where(a => a.Section != null).Select(a => a.Section).Distinct().ToListAsync();
            return View(model);
        }

        // GET: Fees/Create
        public IActionResult Create(int? studentId)
        {
            var students = _context.Students.OrderBy(s => s.Name).ToList();
            ViewBag.Students = students;
            
            var model = new FeeChallan 
            { 
                StudentID = studentId ?? 0,
                DueDate = DateTime.Today.AddDays(10),
                Status = "Unpaid"
            };
            
            if (studentId.HasValue)
            {
                var student = _context.Students.Include(s => s.Admission).FirstOrDefault(s => s.StudentID == studentId);
                if (student != null)
                {
                    model.ClassName = student.Admission?.Class ?? "";
                }
            }
            
            return View(model);
        }

        // POST: Fees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FeeChallan model)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.ChallanID))
                {
                    model.ChallanID = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                }
                
                _context.FeeChallans.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Challans));
            }
            
            ViewBag.Students = _context.Students.OrderBy(s => s.Name).ToList();
            return View(model);
        }

        // GET: Fees/Schedules
        public async Task<IActionResult> Schedules()
        {
            ViewBag.Classes = await _context.Admissions.Where(a => a.Class != null).Select(a => a.Class).Distinct().ToListAsync();
            return View(new FeeSchedule());
        }

        // POST: Fees/SaveSchedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSchedule(FeeSchedule model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.FeeSchedules.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Fee Schedule saved successfully!";
                    return RedirectToAction(nameof(ViewSchedules));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Database Error: " + ex.Message + (ex.InnerException != null ? " | Inner: " + ex.InnerException.Message : ""));
                }
            }

            ViewBag.Classes = await _context.Admissions.Where(a => a.Class != null).Select(a => a.Class).Distinct().ToListAsync();
            return View("Schedules", model);
        }

        // GET: Fees/ViewSchedules
        public async Task<IActionResult> ViewSchedules()
        {
            var schedules = await _context.FeeSchedules
                .Include(s => s.Items)
                .ToListAsync();
            return View(schedules);
        }

        // GET: Fees/Payment
        public IActionResult Payment()
        {
            return View();
        }

        // GET: Fees/BankData
        public IActionResult BankData()
        {
            var months = new List<string>();
            var startDate = new DateTime(2026, 1, 1);
            for (int i = 0; i < 24; i++)
            {
                months.Add(startDate.AddMonths(i).ToString("MMM-yy"));
            }
            ViewBag.Months = months;
            return View();
        }

        // POST: Fees/UpdateArrears
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateArrears(string challanId, decimal arrears)
        {
            var challan = await _context.FeeChallans.FindAsync(challanId);
            if (challan == null)
            {
                return NotFound();
            }

            challan.Arrears = arrears;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Arrears updated for Challan {challanId}.";
            return RedirectToAction(nameof(Challans));
        }

        // GET: Fees/Reports (Dashboard)
        public IActionResult Reports()
        {
            return View();
        }

        // GET: Fees/FeeReport
        public IActionResult FeeReport()
        {
            return View();
        }

        // GET: Fees/FeeBreakdown
        public IActionResult FeeBreakdown()
        {
            return View();
        }

        // GET: Fees/FeeChallanPayment
        public IActionResult FeeChallanPayment()
        {
            return View();
        }
        // GET: Fees/GetStudentDetails/5
        [HttpGet]
        public async Task<IActionResult> GetStudentDetails(int id)
        {
            var student = await _context.Students
                .Include(s => s.Admission)
                .FirstOrDefaultAsync(s => s.StudentID == id);

            if (student == null) return NotFound();

            return Json(new
            {
                className = student.Admission?.Class ?? ""
            });
        }

        // POST: Fees/BulkPay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkPay(string[] challanIds)
        {
            if (challanIds == null || challanIds.Length == 0) return RedirectToAction(nameof(Challans));

            var challans = await _context.FeeChallans.Where(c => challanIds.Contains(c.ChallanID)).ToListAsync();
            foreach (var challan in challans)
            {
                challan.Status = "Paid";
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{challans.Count} challans marked as Paid.";
            return RedirectToAction(nameof(Challans));
        }

        // POST: Fees/BulkUnpaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUnpaid(string[] challanIds)
        {
            if (challanIds == null || challanIds.Length == 0) return RedirectToAction(nameof(Challans));

            var challans = await _context.FeeChallans.Where(c => challanIds.Contains(c.ChallanID)).ToListAsync();
            foreach (var challan in challans)
            {
                challan.Status = "Unpaid";
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{challans.Count} challans marked as Unpaid.";
            return RedirectToAction(nameof(Challans));
        }

        // POST: Fees/BulkDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete(string[] challanIds)
        {
            if (challanIds == null || challanIds.Length == 0) return RedirectToAction(nameof(Challans));

            var challans = await _context.FeeChallans.Where(c => challanIds.Contains(c.ChallanID)).ToListAsync();
            _context.FeeChallans.RemoveRange(challans);

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{challans.Count} challans deleted successfully.";
            return RedirectToAction(nameof(Challans));
        }

        // POST: Fees/BulkModify
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkModify(string[] challanIds, string newMonth, DateTime? newDueDate)
        {
            if (challanIds == null || challanIds.Length == 0) return RedirectToAction(nameof(Challans));

            var challans = await _context.FeeChallans.Where(c => challanIds.Contains(c.ChallanID)).ToListAsync();
            foreach (var challan in challans)
            {
                if (!string.IsNullOrEmpty(newMonth)) challan.Month = newMonth;
                if (newDueDate.HasValue) challan.DueDate = newDueDate.Value;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{challans.Count} challans updated successfully.";
            return RedirectToAction(nameof(Challans));
        }
    }
}
