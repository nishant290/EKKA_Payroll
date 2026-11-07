using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Controllers;

/// <summary>Employer details + the company's bank accounts (debit accounts for paying staff).</summary>
public class CompanyController : TenantController
{
    public CompanyController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var company = await Db.Companies
            .Include(c => c.BankDetails)
            .FirstAsync(c => c.Id == CompanyId);
        return View(company);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        [Bind("Name,TradingName,IrdNumber,Nzbn,Email,Phone,AddressLine1,AddressLine2,Suburb,City,Postcode,PayFrequency")]
        Company input)
    {
        var company = await Db.Companies.Include(c => c.BankDetails).FirstAsync(c => c.Id == CompanyId);
        if (!ModelState.IsValid)
        {
            input.Id = company.Id;
            input.BankDetails = company.BankDetails;
            return View(input);
        }

        company.Name = input.Name;
        company.TradingName = input.TradingName;
        company.IrdNumber = input.IrdNumber;
        company.Nzbn = input.Nzbn;
        company.Email = input.Email;
        company.Phone = input.Phone;
        company.AddressLine1 = input.AddressLine1;
        company.AddressLine2 = input.AddressLine2;
        company.Suburb = input.Suburb;
        company.City = input.City;
        company.Postcode = input.Postcode;
        company.PayFrequency = input.PayFrequency;
        await Db.SaveChangesAsync();

        Success("Company details saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBank([Bind("BankName,AccountName,AccountNumber,IsDefault")] CompanyBankDetail bank)
    {
        if (!ModelState.IsValid)
        {
            Fail("Bank account not saved: " + string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Index));
        }

        bank.CompanyId = CompanyId;
        var hasAny = await Db.CompanyBankDetails.AnyAsync(b => b.CompanyId == CompanyId);
        if (!hasAny) bank.IsDefault = true;
        if (bank.IsDefault)
        {
            var others = await Db.CompanyBankDetails.Where(b => b.CompanyId == CompanyId).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        Db.CompanyBankDetails.Add(bank);
        await Db.SaveChangesAsync();
        Success("Bank account added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBank(int id)
    {
        var bank = await Db.CompanyBankDetails.FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == CompanyId);
        if (bank != null)
        {
            Db.CompanyBankDetails.Remove(bank);
            await Db.SaveChangesAsync();
            Success("Bank account removed.");
        }
        return RedirectToAction(nameof(Index));
    }
}
