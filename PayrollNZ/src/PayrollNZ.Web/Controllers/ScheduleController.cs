using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;
using PayrollNZ.Web.Models.ViewModels;

namespace PayrollNZ.Web.Controllers;

/// <summary>Weekly roster. Shifts are planned work; "Log hours" turns a shift into a timesheet entry.</summary>
public class ScheduleController : TenantController
{
    public ScheduleController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(DateOnly? weekStart)
    {
        var start = HomeController.StartOfWeek(weekStart ?? DateOnly.FromDateTime(DateTime.Today));
        var end = start.AddDays(6);

        var vm = new ScheduleViewModel
        {
            WeekStart = start,
            Employees = await Db.Employees
                .Where(e => e.CompanyId == CompanyId && e.IsActive)
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .ToListAsync(),
            Shifts = await Db.Shifts
                .Where(s => s.Employee!.CompanyId == CompanyId && s.ShiftDate >= start && s.ShiftDate <= end)
                .OrderBy(s => s.StartTime)
                .ToListAsync(),
        };
        return View(vm);
    }

    public async Task<IActionResult> Create(int? employeeId, DateOnly? date)
    {
        ViewData["Employees"] = await EmployeeOptionsAsync(employeeId);
        return View(new Shift
        {
            EmployeeId = employeeId ?? 0,
            ShiftDate = date ?? DateOnly.FromDateTime(DateTime.Today),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("EmployeeId,ShiftDate,StartTime,EndTime,BreakMinutes,Notes")] Shift shift)
    {
        var owns = await Db.Employees.AnyAsync(e => e.Id == shift.EmployeeId && e.CompanyId == CompanyId);
        if (!owns) ModelState.AddModelError(nameof(Shift.EmployeeId), "Choose an employee.");
        if (shift.PlannedHours <= 0)
            ModelState.AddModelError(nameof(Shift.EndTime), "Finish time must be after start time (after the break).");

        if (!ModelState.IsValid)
        {
            ViewData["Employees"] = await EmployeeOptionsAsync(shift.EmployeeId);
            return View(shift);
        }

        Db.Shifts.Add(shift);
        await Db.SaveChangesAsync();
        Success("Shift added.");
        return RedirectToAction(nameof(Index), new { weekStart = shift.ShiftDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var shift = await Db.Shifts.FirstOrDefaultAsync(s => s.Id == id && s.Employee!.CompanyId == CompanyId);
        if (shift == null) return NotFound();

        var week = shift.ShiftDate;
        Db.Shifts.Remove(shift);
        await Db.SaveChangesAsync();
        Success("Shift removed.");
        return RedirectToAction(nameof(Index), new { weekStart = week.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LogHours(int id)
    {
        var shift = await Db.Shifts.FirstOrDefaultAsync(s => s.Id == id && s.Employee!.CompanyId == CompanyId);
        if (shift == null) return NotFound();

        var exists = await Db.Timesheets.AnyAsync(t =>
            t.EmployeeId == shift.EmployeeId && t.WorkDate == shift.ShiftDate && t.StartTime == shift.StartTime);
        if (exists)
        {
            Fail("Hours for this shift are already logged.");
        }
        else
        {
            Db.Timesheets.Add(new TimesheetEntry
            {
                EmployeeId = shift.EmployeeId,
                WorkDate = shift.ShiftDate,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                BreakMinutes = shift.BreakMinutes,
                Notes = shift.Notes,
                Hours = shift.PlannedHours,
            });
            await Db.SaveChangesAsync();
            Success("Shift copied to timesheet.");
        }
        return RedirectToAction(nameof(Index), new { weekStart = shift.ShiftDate.ToString("yyyy-MM-dd") });
    }

    private async Task<List<SelectListItem>> EmployeeOptionsAsync(int? selected)
    {
        var emps = await Db.Employees
            .Where(e => e.CompanyId == CompanyId && e.IsActive)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToListAsync();
        return emps.Select(e => new SelectListItem(e.FullName, e.Id.ToString(), e.Id == selected)).ToList();
    }
}
