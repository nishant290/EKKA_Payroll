using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;
using PayrollNZ.Web.Models.ViewModels;
using PayrollNZ.Web.Services;

namespace PayrollNZ.Web.Controllers;

public class PayRunsController : TenantController
{
    private readonly IPayRunService _runs;
    private readonly IPayslipPdfService _pdf;
    private readonly IEmailService _email;
    private readonly ILogger<PayRunsController> _log;

    public PayRunsController(AppDbContext db, IPayRunService runs, IPayslipPdfService pdf,
                             IEmailService email, ILogger<PayRunsController> log) : base(db)
    {
        _runs = runs;
        _pdf = pdf;
        _email = email;
        _log = log;
    }

    public async Task<IActionResult> Index()
    {
        var list = await Db.PayRuns
            .Include(r => r.Payslips)
            .Where(r => r.CompanyId == CompanyId)
            .OrderByDescending(r => r.PeriodEnd)
            .ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Create()
    {
        // Suggest the period following the last pay run, based on the company's pay frequency.
        var company = await Db.Companies.FirstAsync(c => c.Id == CompanyId);
        var last = await Db.PayRuns.Where(r => r.CompanyId == CompanyId)
            .OrderByDescending(r => r.PeriodEnd).FirstOrDefaultAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = company.PayFrequency switch
        {
            PayFrequency.Weekly => 7,
            PayFrequency.Fortnightly => 14,
            _ => 0,
        };

        DateOnly start, end;
        if (days == 0)
        {
            start = last != null ? last.PeriodEnd.AddDays(1) : new DateOnly(today.Year, today.Month, 1);
            end = start.AddMonths(1).AddDays(-1);
        }
        else
        {
            start = last != null ? last.PeriodEnd.AddDays(1) : HomeController.StartOfWeek(today).AddDays(-days);
            end = start.AddDays(days - 1);
        }

        return View(new PayRunCreateViewModel { PeriodStart = start, PeriodEnd = end, PaymentDate = end.AddDays(3) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PayRunCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (run, error) = await _runs.CreateDraftAsync(CompanyId, model.PeriodStart, model.PeriodEnd, model.PaymentDate);
        if (run == null)
        {
            ModelState.AddModelError("", error ?? "Could not create pay run.");
            return View(model);
        }

        Success("Draft pay run created. Review it, then post it.");
        return RedirectToAction(nameof(Details), new { id = run.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var run = await LoadRunAsync(id);
        return run == null ? NotFound() : View(run);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int id)
    {
        var error = await _runs.PostAsync(CompanyId, id);
        if (error != null) Fail(error); else Success("Pay run posted. You can now email payslips.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await _runs.DeleteDraftAsync(CompanyId, id);
        if (error != null)
        {
            Fail(error);
            return RedirectToAction(nameof(Details), new { id });
        }
        Success("Draft pay run deleted.");
        return RedirectToAction(nameof(Index));
    }

    // ---- Payslips ----

    public async Task<IActionResult> PayslipPdf(int id)
    {
        var slip = await LoadPayslipAsync(id);
        if (slip == null) return NotFound();

        var bytes = _pdf.Generate(slip);
        return File(bytes, "application/pdf", PdfFileName(slip));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailPayslip(int id)
    {
        var slip = await LoadPayslipAsync(id);
        if (slip == null) return NotFound();
        if (slip.PayRun!.Status != PayRunStatus.Posted)
        {
            Fail("Post the pay run before emailing payslips.");
            return RedirectToAction(nameof(Details), new { id = slip.PayRunId });
        }

        try
        {
            await SendSlipAsync(slip);
            await Db.SaveChangesAsync();
            Success($"Payslip emailed to {slip.Employee!.Email}.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to email payslip {PayslipId}", id);
            Fail("Could not send email: " + ex.Message);
        }
        return RedirectToAction(nameof(Details), new { id = slip.PayRunId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailAll(int id)
    {
        var run = await LoadRunAsync(id);
        if (run == null) return NotFound();
        if (run.Status != PayRunStatus.Posted)
        {
            Fail("Post the pay run before emailing payslips.");
            return RedirectToAction(nameof(Details), new { id });
        }

        int sent = 0, failed = 0;
        string? firstError = null;
        foreach (var slip in run.Payslips)
        {
            try
            {
                await SendSlipAsync(slip);
                sent++;
            }
            catch (InvalidOperationException ex)   // email not configured: no point trying the rest
            {
                Fail(ex.Message);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                failed++;
                firstError ??= ex.Message;
                _log.LogError(ex, "Failed to email payslip {PayslipId}", slip.Id);
            }
        }
        await Db.SaveChangesAsync();

        if (failed == 0) Success($"{sent} payslip(s) emailed.");
        else Fail($"{sent} sent, {failed} failed. First error: {firstError}");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- helpers ----

    private async Task SendSlipAsync(Payslip slip)
    {
        var emp = slip.Employee!;
        var run = slip.PayRun!;
        var company = run.Company!;
        var pdf = _pdf.Generate(slip);

        var body = $@"<p>Hi {System.Net.WebUtility.HtmlEncode(emp.FirstName)},</p>
<p>Your payslip for <strong>{run.PeriodStart:dd MMM yyyy} – {run.PeriodEnd:dd MMM yyyy}</strong> is attached.</p>
<p>Net pay: <strong>{slip.NetPay:C2}</strong>, paid on {run.PaymentDate:dd MMM yyyy}.</p>
<p>Regards,<br/>{System.Net.WebUtility.HtmlEncode(company.TradingName ?? company.Name)}</p>";

        await _email.SendAsync(emp.Email, emp.FullName,
            $"Payslip {run.PeriodStart:dd MMM} – {run.PeriodEnd:dd MMM yyyy}", body, PdfFileName(slip), pdf);
        slip.EmailedAtUtc = DateTime.UtcNow;
    }

    private static string PdfFileName(Payslip slip) =>
        $"Payslip_{slip.Employee!.LastName}_{slip.PayRun!.PeriodEnd:yyyyMMdd}.pdf";

    private Task<PayRun?> LoadRunAsync(int id) =>
        Db.PayRuns
            .Include(r => r.Company)
            .Include(r => r.Payslips).ThenInclude(p => p.Employee).ThenInclude(e => e!.BankDetails)
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == CompanyId);

    private Task<Payslip?> LoadPayslipAsync(int id) =>
        Db.Payslips
            .Include(p => p.Employee).ThenInclude(e => e!.BankDetails)
            .Include(p => p.PayRun).ThenInclude(r => r!.Company)
            .FirstOrDefaultAsync(p => p.Id == id && p.PayRun!.CompanyId == CompanyId);
}
