using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;
using PayrollNZ.Web.Models.ViewModels;

namespace PayrollNZ.Web.Controllers;

public class HomeController : TenantController
{
    public HomeController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = StartOfWeek(today);
        var weekEnd = weekStart.AddDays(6);

        var vm = new DashboardViewModel
        {
            CompanyName = (await Db.Companies.FindAsync(CompanyId))?.Name ?? "",
            ActiveEmployees = await Db.Employees.CountAsync(e => e.CompanyId == CompanyId && e.IsActive),
            HoursThisWeek = await Db.Timesheets
                .Where(t => t.Employee!.CompanyId == CompanyId && t.WorkDate >= weekStart && t.WorkDate <= weekEnd)
                .SumAsync(t => (decimal?)t.Hours) ?? 0m,
            ShiftsThisWeek = await Db.Shifts
                .CountAsync(s => s.Employee!.CompanyId == CompanyId && s.ShiftDate >= weekStart && s.ShiftDate <= weekEnd),
            LastPayRun = await Db.PayRuns
                .Include(r => r.Payslips)
                .Where(r => r.CompanyId == CompanyId)
                .OrderByDescending(r => r.PeriodEnd)
                .FirstOrDefaultAsync(),
            UpcomingShifts = await Db.Shifts
                .Include(s => s.Employee)
                .Where(s => s.Employee!.CompanyId == CompanyId && s.ShiftDate >= today)
                .OrderBy(s => s.ShiftDate).ThenBy(s => s.StartTime)
                .Take(8)
                .ToListAsync(),
        };
        return View(vm);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    public static DateOnly StartOfWeek(DateOnly d)
    {
        // Monday-based week (NZ convention).
        int diff = ((int)d.DayOfWeek + 6) % 7;
        return d.AddDays(-diff);
    }
}
