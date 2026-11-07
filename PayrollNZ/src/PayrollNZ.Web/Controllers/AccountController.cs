using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;
using PayrollNZ.Web.Models.ViewModels;

namespace PayrollNZ.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AppDbContext _db;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", result.IsLockedOut
            ? "Account locked after too many attempts. Try again in a few minutes."
            : "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Sign-up creates the employer (company) and its first user together.
        var company = new Company { Name = model.CompanyName.Trim(), Email = model.Email };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            CompanyId = company.Id,
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            _db.Companies.Remove(company);          // roll back the orphan company
            await _db.SaveChangesAsync();
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Company");   // first stop: finish company details
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
