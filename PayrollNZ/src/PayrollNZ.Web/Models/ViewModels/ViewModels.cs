using System.ComponentModel.DataAnnotations;

namespace PayrollNZ.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(100), Display(Name = "Your name")]
    public string FullName { get; set; } = "";

    [Required, StringLength(150), Display(Name = "Company name")]
    public string CompanyName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class DashboardViewModel
{
    public string CompanyName { get; set; } = "";
    public int ActiveEmployees { get; set; }
    public decimal HoursThisWeek { get; set; }
    public int ShiftsThisWeek { get; set; }
    public PayRun? LastPayRun { get; set; }
    public List<Shift> UpcomingShifts { get; set; } = new();
}

public class ScheduleViewModel
{
    public DateOnly WeekStart { get; set; }
    public List<Employee> Employees { get; set; } = new();
    public List<Shift> Shifts { get; set; } = new();
}

public class PayRunCreateViewModel
{
    [Required, DataType(DataType.Date), Display(Name = "Period start")]
    public DateOnly PeriodStart { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Period end")]
    public DateOnly PeriodEnd { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Payment date")]
    public DateOnly PaymentDate { get; set; }
}
