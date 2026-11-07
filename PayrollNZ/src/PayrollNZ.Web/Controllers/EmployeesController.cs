using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Controllers;

public class EmployeesController : TenantController
{
    // Whitelist of bindable fields (prevents over-posting of Id / CompanyId).
    private const string EditableFields =
        "FirstName,LastName,Email,Phone,DateOfBirth,AddressLine1,AddressLine2,Suburb,City,Postcode,Country," +
        "EmergencyContactName,EmergencyContactPhone,JobTitle,EmploymentType,PayType,HourlyRate,AnnualSalary," +
        "StartDate,EndDate,IsActive,IrdNumber,TaxCode,HasStudentLoan,KiwiSaverEmployeeRate,KiwiSaverEmployerRate";

    public EmployeesController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string? q, bool showInactive = false)
    {
        var query = Db.Employees.Where(e => e.CompanyId == CompanyId);
        if (!showInactive) query = query.Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.FirstName.Contains(term) || e.LastName.Contains(term) || e.Email.Contains(term));
        }

        ViewData["Query"] = q;
        ViewData["ShowInactive"] = showInactive;
        return View(await query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var emp = await Db.Employees.Include(e => e.BankDetails)
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == CompanyId);
        if (emp == null) return NotFound();

        ViewData["Payslips"] = await Db.Payslips
            .Include(p => p.PayRun)
            .Where(p => p.EmployeeId == id)
            .OrderByDescending(p => p.PayRun!.PeriodEnd)
            .Take(10)
            .ToListAsync();
        return View(emp);
    }

    public IActionResult Create() => View(new Employee());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(EditableFields)] Employee employee)
    {
        ValidateEmployee(employee);
        if (!ModelState.IsValid) return View(employee);

        employee.CompanyId = CompanyId;
        Db.Employees.Add(employee);
        await Db.SaveChangesAsync();
        Success($"{employee.FullName} added. Now add their bank details.");
        return RedirectToAction(nameof(Details), new { id = employee.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var emp = await Db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == CompanyId);
        return emp == null ? NotFound() : View(emp);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(EditableFields)] Employee input)
    {
        var emp = await Db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == CompanyId);
        if (emp == null) return NotFound();

        ValidateEmployee(input);
        if (!ModelState.IsValid)
        {
            input.Id = id;
            return View(input);
        }

        // Copy only the whitelisted fields onto the tracked entity.
        emp.FirstName = input.FirstName;
        emp.LastName = input.LastName;
        emp.Email = input.Email;
        emp.Phone = input.Phone;
        emp.DateOfBirth = input.DateOfBirth;
        emp.AddressLine1 = input.AddressLine1;
        emp.AddressLine2 = input.AddressLine2;
        emp.Suburb = input.Suburb;
        emp.City = input.City;
        emp.Postcode = input.Postcode;
        emp.Country = input.Country;
        emp.EmergencyContactName = input.EmergencyContactName;
        emp.EmergencyContactPhone = input.EmergencyContactPhone;
        emp.JobTitle = input.JobTitle;
        emp.EmploymentType = input.EmploymentType;
        emp.PayType = input.PayType;
        emp.HourlyRate = input.HourlyRate;
        emp.AnnualSalary = input.AnnualSalary;
        emp.StartDate = input.StartDate;
        emp.EndDate = input.EndDate;
        emp.IsActive = input.IsActive;
        emp.IrdNumber = input.IrdNumber;
        emp.TaxCode = input.TaxCode;
        emp.HasStudentLoan = input.HasStudentLoan;
        emp.KiwiSaverEmployeeRate = input.KiwiSaverEmployeeRate;
        emp.KiwiSaverEmployerRate = input.KiwiSaverEmployerRate;
        await Db.SaveChangesAsync();

        Success("Employee updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var emp = await Db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == CompanyId);
        if (emp == null) return NotFound();

        emp.IsActive = !emp.IsActive;
        if (!emp.IsActive && emp.EndDate == null) emp.EndDate = DateOnly.FromDateTime(DateTime.Today);
        if (emp.IsActive) emp.EndDate = null;
        await Db.SaveChangesAsync();

        Success($"{emp.FullName} is now {(emp.IsActive ? "active" : "inactive")}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Employee bank accounts ----

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBank(int id,
        [Bind("BankName,AccountName,AccountNumber,Particulars,Code,Reference,IsPrimary")] EmployeeBankDetail bank)
    {
        var emp = await Db.Employees.Include(e => e.BankDetails)
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == CompanyId);
        if (emp == null) return NotFound();

        if (!ModelState.IsValid)
        {
            Fail("Bank account not saved: " + string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Details), new { id });
        }

        if (emp.BankDetails.Count == 0) bank.IsPrimary = true;
        if (bank.IsPrimary) foreach (var b in emp.BankDetails) b.IsPrimary = false;

        bank.EmployeeId = id;
        Db.EmployeeBankDetails.Add(bank);
        await Db.SaveChangesAsync();
        Success("Bank account added.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBank(int id, int bankId)
    {
        var bank = await Db.EmployeeBankDetails
            .FirstOrDefaultAsync(b => b.Id == bankId && b.EmployeeId == id && b.Employee!.CompanyId == CompanyId);
        if (bank != null)
        {
            Db.EmployeeBankDetails.Remove(bank);
            await Db.SaveChangesAsync();
            Success("Bank account removed.");
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private void ValidateEmployee(Employee e)
    {
        if (e.PayType == PayType.Hourly && e.HourlyRate <= 0)
            ModelState.AddModelError(nameof(Employee.HourlyRate), "Enter an hourly rate for hourly employees.");
        if (e.PayType == PayType.Salary && e.AnnualSalary <= 0)
            ModelState.AddModelError(nameof(Employee.AnnualSalary), "Enter an annual salary for salaried employees.");
        if (e.EndDate != null && e.EndDate < e.StartDate)
            ModelState.AddModelError(nameof(Employee.EndDate), "End date cannot be before start date.");
    }
}
