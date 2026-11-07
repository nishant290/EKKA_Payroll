using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Controllers;

public class TimesheetsController : TenantController
{
    public TimesheetsController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(int? employeeId, DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = from ?? HomeController.StartOfWeek(today);
        var end = to ?? start.AddDays(6);

        var query = Db.Timesheets.Include(t => t.Employee)
            .Where(t => t.Employee!.CompanyId == CompanyId && t.WorkDate >= start && t.WorkDate <= end);
        if (employeeId != null) query = query.Where(t => t.EmployeeId == employeeId);

        ViewData["From"] = start;
        ViewData["To"] = end;
        ViewData["EmployeeId"] = employeeId;
        ViewData["Employees"] = await EmployeeOptionsAsync(employeeId);

        var list = await query.OrderByDescending(t => t.WorkDate).ThenBy(t => t.StartTime).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Create(int? employeeId)
    {
        ViewData["Employees"] = await EmployeeOptionsAsync(employeeId);
        return View(new TimesheetEntry { EmployeeId = employeeId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("EmployeeId,WorkDate,StartTime,EndTime,BreakMinutes,Notes")] TimesheetEntry entry)
    {
        var owns = await Db.Employees.AnyAsync(e => e.Id == entry.EmployeeId && e.CompanyId == CompanyId);
        if (!owns) ModelState.AddModelError(nameof(TimesheetEntry.EmployeeId), "Choose an employee.");

        entry.Hours = TimesheetEntry.CalculateHours(entry.StartTime, entry.EndTime, entry.BreakMinutes);
        if (entry.Hours <= 0)
            ModelState.AddModelError(nameof(TimesheetEntry.EndTime), "Finish time must be after start time (after the break).");

        if (!ModelState.IsValid)
        {
            ViewData["Employees"] = await EmployeeOptionsAsync(entry.EmployeeId);
            return View(entry);
        }

        Db.Timesheets.Add(entry);
        await Db.SaveChangesAsync();
        Success($"Logged {entry.Hours:0.##} hours.");
        return RedirectToAction(nameof(Index), new { employeeId = entry.EmployeeId, from = entry.WorkDate.ToString("yyyy-MM-dd"), to = entry.WorkDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await Db.Timesheets
            .FirstOrDefaultAsync(t => t.Id == id && t.Employee!.CompanyId == CompanyId);
        if (entry == null) return NotFound();
        if (entry.PayRunId != null)
        {
            Fail("This entry is already part of a pay run. Delete the draft pay run first.");
            return RedirectToAction(nameof(Index));
        }

        Db.Timesheets.Remove(entry);
        await Db.SaveChangesAsync();
        Success("Entry deleted.");
        return RedirectToAction(nameof(Index));
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
