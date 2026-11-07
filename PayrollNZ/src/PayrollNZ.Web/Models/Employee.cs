using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollNZ.Web.Models;

public class Employee
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    // ---- Personal ----
    [Required, StringLength(80), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(80), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";

    [StringLength(30), Display(Name = "Phone")]
    public string? Phone { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    // ---- Residential address ----
    [StringLength(150), Display(Name = "Address line 1")]
    public string? AddressLine1 { get; set; }

    [StringLength(150), Display(Name = "Address line 2")]
    public string? AddressLine2 { get; set; }

    [StringLength(80)]
    public string? Suburb { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(10)]
    public string? Postcode { get; set; }

    [StringLength(80)]
    public string Country { get; set; } = "New Zealand";

    // ---- Emergency contact ----
    [StringLength(100), Display(Name = "Emergency contact name")]
    public string? EmergencyContactName { get; set; }

    [StringLength(30), Display(Name = "Emergency contact phone")]
    public string? EmergencyContactPhone { get; set; }

    // ---- Employment ----
    [StringLength(100), Display(Name = "Job title")]
    public string? JobTitle { get; set; }

    [Display(Name = "Employment type")]
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    [Display(Name = "Pay type")]
    public PayType PayType { get; set; } = PayType.Hourly;

    [Range(0, 10000), Display(Name = "Hourly rate ($)")]
    public decimal HourlyRate { get; set; }

    [Range(0, 10000000), Display(Name = "Annual salary ($)")]
    public decimal AnnualSalary { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Start date")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date), Display(Name = "End date")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // ---- Tax (basic; full NZ tax engine comes in the next phase) ----
    [StringLength(15), Display(Name = "IRD number")]
    public string? IrdNumber { get; set; }

    [Required, StringLength(10), Display(Name = "Tax code")]
    public string TaxCode { get; set; } = "M";

    [Display(Name = "Has student loan")]
    public bool HasStudentLoan { get; set; }

    [Range(0, 100), Display(Name = "KiwiSaver employee rate (%)")]
    public decimal KiwiSaverEmployeeRate { get; set; } = 3m;

    [Range(0, 100), Display(Name = "KiwiSaver employer rate (%)")]
    public decimal KiwiSaverEmployerRate { get; set; } = 3m;

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    public ICollection<EmployeeBankDetail> BankDetails { get; set; } = new List<EmployeeBankDetail>();
    public ICollection<TimesheetEntry> Timesheets { get; set; } = new List<TimesheetEntry>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}

/// <summary>Employee bank account (the credit account their pay is deposited into).</summary>
public class EmployeeBankDetail
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required, StringLength(60), Display(Name = "Bank")]
    public string BankName { get; set; } = "";

    [Required, StringLength(100), Display(Name = "Account name")]
    public string AccountName { get; set; } = "";

    [Required, StringLength(25), Display(Name = "Account number")]
    [RegularExpression(@"^\d{2}-\d{4}-\d{7}-\d{2,3}$", ErrorMessage = "Use NZ format: 12-3456-1234567-00")]
    public string AccountNumber { get; set; } = "";

    [StringLength(12)]
    public string? Particulars { get; set; }

    [StringLength(12)]
    public string? Code { get; set; }

    [StringLength(12)]
    public string? Reference { get; set; }

    [Display(Name = "Primary account")]
    public bool IsPrimary { get; set; }
}
