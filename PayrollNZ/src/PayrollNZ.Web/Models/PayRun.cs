using System.ComponentModel.DataAnnotations;

namespace PayrollNZ.Web.Models;

public class PayRun
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Period start")]
    public DateOnly PeriodStart { get; set; }

    [Display(Name = "Period end")]
    public DateOnly PeriodEnd { get; set; }

    [Display(Name = "Payment date")]
    public DateOnly PaymentDate { get; set; }

    public PayFrequency Frequency { get; set; }
    public PayRunStatus Status { get; set; } = PayRunStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAtUtc { get; set; }

    public ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();

    public decimal TotalGross => Payslips.Sum(p => p.GrossPay);
    public decimal TotalTax => Payslips.Sum(p => p.PayeTax);
    public decimal TotalNet => Payslips.Sum(p => p.NetPay);
}

public class Payslip
{
    public int Id { get; set; }
    public int PayRunId { get; set; }
    public PayRun? PayRun { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal GrossPay { get; set; }
    public decimal PayeTax { get; set; }
    public decimal StudentLoan { get; set; }
    public decimal KiwiSaverEmployee { get; set; }
    public decimal KiwiSaverEmployer { get; set; }
    public decimal AccLevy { get; set; }
    public decimal NetPay { get; set; }

    /// <summary>Snapshot of the tax code used, so later changes to the employee don't alter history.</summary>
    [StringLength(10)]
    public string TaxCode { get; set; } = "M";

    public DateTime? EmailedAtUtc { get; set; }
}
