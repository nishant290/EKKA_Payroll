using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;

namespace PayrollNZ.Web.Controllers;

/// <summary>
/// Base for all signed-in controllers. Resolves the current user's company once per request so every
/// query can be scoped to it (multi-tenant safety: a user must never see another company's data).
/// </summary>
public abstract class TenantController : Controller
{
    protected readonly AppDbContext Db;

    protected TenantController(AppDbContext db) => Db = db;

    protected int CompanyId { get; private set; }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var companyId = await Db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync();

        if (companyId == null)
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        CompanyId = companyId.Value;
        await base.OnActionExecutionAsync(context, next);
    }

    protected void Success(string message) => TempData["Success"] = message;
    protected void Fail(string message) => TempData["Error"] = message;
}
