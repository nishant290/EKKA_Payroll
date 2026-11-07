using System.ComponentModel.DataAnnotations;

namespace PayrollNZ.Web.Models;

/// <summary>The employer. Every user, employee and pay run belongs to exactly one company (tenant).</summary>
public class Company
{
    public int Id { get; set; }

    [Required, StringLength(150), Display(Name = "Legal name")]
    public string Name { get; set; } = "";

    [StringLength(150), Display(Name = "Trading name")]
    public string? TradingName { get; set; }

    [StringLength(20), Display(Name = "Employer IRD number")]
    public string? IrdNumber { get; set; }

    [StringLength(30), Display(Name = "NZBN")]
    public string? Nzbn { get; set; }

    [EmailAddress, StringLength(150), Display(Name = "Contact email")]
    public string? Email { get; set; }

    [StringLength(30), Display(Name = "Phone")]
    public string? Phone { get; set; }

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

    [Display(Name = "Default pay frequency")]
    public PayFrequency PayFrequency { get; set; } = PayFrequency.Fortnightly;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CompanyBankDetail> BankDetails { get; set; } = new List<CompanyBankDetail>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<PayRun> PayRuns { get; set; } = new List<PayRun>();
}

/// <summary>Employer bank account (the debit account used when paying staff).</summary>
public class CompanyBankDetail
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, StringLength(60), Display(Name = "Bank")]
    public string BankName { get; set; } = "";

    [Required, StringLength(100), Display(Name = "Account name")]
    public string AccountName { get; set; } = "";

    [Required, StringLength(25), Display(Name = "Account number")]
    [RegularExpression(@"^\d{2}-\d{4}-\d{7}-\d{2,3}$", ErrorMessage = "Use NZ format: 12-3456-1234567-00")]
    public string AccountNumber { get; set; } = "";

    [Display(Name = "Default account")]
    public bool IsDefault { get; set; }
}
