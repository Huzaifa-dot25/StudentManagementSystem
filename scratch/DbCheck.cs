using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using System.Linq;
using System.Threading.Tasks;

public class DbCheck
{
    private readonly ApplicationDbContext _context;
    public DbCheck(ApplicationDbContext context) { _context = context; }

    public async Task Check()
    {
        var admSessions = await _context.Admissions.Select(a => a.Session).Distinct().ToListAsync();
        var resSessions = await _context.StudentResults.Select(r => r.Session).Distinct().ToListAsync();
        
        System.Console.WriteLine("Admissions Sessions: " + string.Join(", ", admSessions.Select(s => s ?? "NULL")));
        System.Console.WriteLine("Results Sessions: " + string.Join(", ", resSessions.Select(s => s ?? "NULL")));
    }
}
