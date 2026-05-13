using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.Models;
using StudentManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace StudentManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var sixMonthsAgo = DateTime.Today.AddMonths(-6);
        var nextWeek = DateTime.Today.AddDays(7);

        var model = new DashboardViewModel
        {
            TotalStudents = await _context.Students.CountAsync(),
            ActiveStudents = await _context.Students.CountAsync(s => s.Status == "Active"),
            
            TotalRevenue = await _context.FeeChallans
                .Where(fc => fc.Status == "Paid")
                .SumAsync(fc => (decimal?)fc.Amount) ?? 0,
                
            PendingFees = await _context.FeeChallans
                .Where(fc => fc.Status == "Unpaid")
                .SumAsync(fc => (decimal?)fc.Amount) ?? 0,
                
            MaleStudentsCount = await _context.Students.CountAsync(s => s.Gender == "Male"),
            FemaleStudentsCount = await _context.Students.CountAsync(s => s.Gender == "Female"),
            
            PaidChallansCount = await _context.FeeChallans.CountAsync(fc => fc.Status == "Paid"),
            UnpaidChallansCount = await _context.FeeChallans.CountAsync(fc => fc.Status == "Unpaid"),

            StaffChildCount = await _context.Students.CountAsync(s => s.StaffChild == "Yes"),
            RegularStudentCount = await _context.Students.CountAsync(s => s.StaffChild != "Yes"),
            
            RecentStudents = await _context.Students
                .OrderByDescending(s => s.RegistrationDate)
                .Take(5)
                .ToListAsync(),
                
            RecentUnpaidChallans = await _context.FeeChallans
                .Include(fc => fc.Student)
                .Where(fc => fc.Status == "Unpaid")
                .OrderBy(fc => fc.DueDate)
                .Take(5)
                .ToListAsync(),

            UpcomingDeadlines = await _context.FeeChallans
                .Include(fc => fc.Student)
                .Where(fc => fc.Status == "Unpaid" && fc.DueDate <= nextWeek && fc.DueDate >= DateTime.Today)
                .OrderBy(fc => fc.DueDate)
                .Take(5)
                .ToListAsync()
        };

        // Monthly Admissions Data (Last 6 Months)
        var monthlyData = await _context.Students
            .Where(s => s.RegistrationDate >= sixMonthsAgo)
            .GroupBy(s => new { s.RegistrationDate.Year, s.RegistrationDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        foreach (var item in monthlyData)
        {
            model.MonthlyAdmissionsLabels.Add(new DateTime(item.Year, item.Month, 1).ToString("MMM yy"));
            model.MonthlyAdmissionsData.Add(item.Count);
        }

        // Class Distribution Data
        var classData = await _context.Admissions
            .Where(a => !string.IsNullOrEmpty(a.Class))
            .GroupBy(a => a.Class)
            .Select(g => new { ClassName = g.Key, Count = g.Count() })
            .OrderBy(x => x.ClassName)
            .ToListAsync();

        foreach (var item in classData)
        {
            model.ClassDistributionLabels.Add(item.ClassName ?? "Unknown");
            model.ClassDistributionData.Add(item.Count);
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
