using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Services;

public interface IPayRunService
{
    Task<(PayRun? Run, string? Error)> CreateDraftAsync(int companyId, DateOnly start, DateOnly end, DateOnly paymentDate);
    Task<string?> PostAsync(int companyId, int payRunId);
    Task<string?> DeleteDraftAsync(int companyId, int payRunId);
}

public class PayRunService : IPayRunService
{
    private readonly AppDbContext _db;
    private readonly IPayrollCalculationService _calc;

    public PayRunService(AppDbContext db, IPayrollCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    public async Task<(PayRun? Run, string? Error)> CreateDraftAsync(
        int companyId, DateOnly start, DateOnly end, DateOnly paymentDate)
    {
        if (end < start) return (null, "Period end must be on or after period start.");

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
        if (company == null) return (null, "Company not found.");

        var overlaps = await _db.PayRuns.AnyAsync(r =>
            r.CompanyId == companyId && r.PeriodStart <= end && r.PeriodEnd >= start);
        if (overlaps) return (null, "This period overlaps an existing pay run.");

        var employees = await _db.Employees
            .Where(e => e.CompanyId == companyId && e.IsActive && e.StartDate <= end
                        && (e.EndDate == null || e.EndDate >= start))
            .ToListAsync();
        if (employees.Count == 0) return (null, "There are no active employees to pay in this period.");

        var empIds = employees.Select(e => e.Id).ToList();
        var entries = await _db.Timesheets
            .Where(t => empIds.Contains(t.EmployeeId) && t.PayRunId == null
                        && t.WorkDate >= start && t.WorkDate <= end)
            .ToListAsync();

        var run = new PayRun
        {
            CompanyId = companyId,
            PeriodStart = start,
            PeriodEnd = end,
            PaymentDate = paymentDate,
            Frequency = company.PayFrequency,
            Status = PayRunStatus.Draft,
        };

        foreach (var emp in employees)
        {
            var mine = entries.Where(t => t.EmployeeId == emp.Id).ToList();
            var slip = _calc.Calculate(emp, mine, company.PayFrequency);
            if (slip.GrossPay <= 0 && mine.Count == 0) continue;   // nothing to pay this period
            run.Payslips.Add(slip);
        }

        if (run.Payslips.Count == 0)
            return (null, "No employee has hours or salary to pay in this period. Add timesheet entries first.");

        _db.PayRuns.Add(run);
        await _db.SaveChangesAsync();

        // Lock the timesheet entries into this run so they can't be paid twice.
        foreach (var t in entries.Where(t => run.Payslips.Any(p => p.EmployeeId == t.EmployeeId)))
            t.PayRunId = run.Id;
        await _db.SaveChangesAsync();

        return (run, null);
    }

    public async Task<string?> PostAsync(int companyId, int payRunId)
    {
        var run = await _db.PayRuns.FirstOrDefaultAsync(r => r.Id == payRunId && r.CompanyId == companyId);
        if (run == null) return "Pay run not found.";
        if (run.Status == PayRunStatus.Posted) return "Pay run is already posted.";

        run.Status = PayRunStatus.Posted;
        run.PostedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteDraftAsync(int companyId, int payRunId)
    {
        var run = await _db.PayRuns.FirstOrDefaultAsync(r => r.Id == payRunId && r.CompanyId == companyId);
        if (run == null) return "Pay run not found.";
        if (run.Status != PayRunStatus.Draft) return "Only draft pay runs can be deleted.";

        var locked = await _db.Timesheets.Where(t => t.PayRunId == run.Id).ToListAsync();
        foreach (var t in locked) t.PayRunId = null;

        _db.PayRuns.Remove(run);
        await _db.SaveChangesAsync();
        return null;
    }
}
