using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Data;
using PayrollNZ.Web.Models;
using PayrollNZ.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (MySQL via Pomelo) ----
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

// ---- Identity (sign in / sign up) ----
builder.Services
    .AddIdentity<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>();
    

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});

// ---- MVC: everything requires login unless marked [AllowAnonymous] ----
builder.Services.AddControllersWithViews(o =>
{
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.Filters.Add(new AuthorizeFilter(policy));
});

// ---- App services ----
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<ITaxCalculator, BasicTaxCalculator>();
builder.Services.AddScoped<IPayrollCalculationService, PayrollCalculationService>();
builder.Services.AddScoped<IPayRunService, PayRunService>();
builder.Services.AddSingleton<IPayslipPdfService, PayslipPdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// Apply pending EF migrations on startup (create the first migration with `dotnet ef migrations add InitialCreate`).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
