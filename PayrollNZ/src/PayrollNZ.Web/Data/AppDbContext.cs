using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyBankDetail> CompanyBankDetails => Set<CompanyBankDetail>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeBankDetail> EmployeeBankDetails => Set<EmployeeBankDetail>();
    public DbSet<TimesheetEntry> Timesheets => Set<TimesheetEntry>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<PayRun> PayRuns => Set<PayRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money / hours: 2 decimal places everywhere unless overridden below.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>()
            .HasOne(u => u.Company).WithMany().HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Employee>(e =>
        {
            e.HasOne(x => x.Company).WithMany(c => c.Employees).HasForeignKey(x => x.CompanyId);
            e.Property(x => x.HourlyRate).HasPrecision(18, 4);
            e.HasIndex(x => new { x.CompanyId, x.Email });
        });

        b.Entity<Payslip>(p =>
        {
            // Never silently delete payslip history if an employee row is removed.
            p.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            p.HasOne(x => x.PayRun).WithMany(r => r.Payslips).HasForeignKey(x => x.PayRunId)
                .OnDelete(DeleteBehavior.Cascade);
            p.Property(x => x.HourlyRate).HasPrecision(18, 4);
        });

        b.Entity<TimesheetEntry>(t =>
        {
            t.HasOne(x => x.Employee).WithMany(e => e.Timesheets).HasForeignKey(x => x.EmployeeId);
            t.HasIndex(x => new { x.EmployeeId, x.WorkDate });
        });

        b.Entity<Shift>(s =>
        {
            s.HasOne(x => x.Employee).WithMany(e => e.Shifts).HasForeignKey(x => x.EmployeeId);
            s.Ignore(x => x.PlannedHours);
            s.HasIndex(x => new { x.EmployeeId, x.ShiftDate });
        });

        b.Entity<PayRun>(r =>
        {
            r.Ignore(x => x.TotalGross);
            r.Ignore(x => x.TotalTax);
            r.Ignore(x => x.TotalNet);
        });
    }
}
