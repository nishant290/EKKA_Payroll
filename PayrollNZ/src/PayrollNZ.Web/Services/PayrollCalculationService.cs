using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Services;

public interface IPayrollCalculationService
{
    /// <summary>Builds a (not yet saved) payslip for one employee and one period.</summary>
    Payslip Calculate(Employee employee, IReadOnlyCollection<TimesheetEntry> entries, PayFrequency frequency);
}

/// <summary>
/// Basic gross-to-net. Phase 2 extends this with public holidays, annual/sick/bereavement leave,
/// alternative holidays, RDP/ADP and leave-balance updates.
/// </summary>
public class PayrollCalculationService : IPayrollCalculationService
{
    private readonly ITaxCalculator _tax;

    public PayrollCalculationService(ITaxCalculator tax) => _tax = tax;

    public Payslip Calculate(Employee employee, IReadOnlyCollection<TimesheetEntry> entries, PayFrequency frequency)
    {
        decimal hours = entries.Sum(e => e.Hours);
        decimal gross;
        decimal rate;

        if (employee.PayType == PayType.Salary)
        {
            gross = Math.Round(employee.AnnualSalary / BasicTaxCalculator.PeriodsPerYear(frequency), 2);
            rate = 0m;
        }
        else
        {
            rate = employee.HourlyRate;
            gross = Math.Round(hours * rate, 2);
        }

        var t = _tax.Calculate(employee, gross, frequency);
        var net = gross - t.Paye - t.StudentLoan - t.KiwiSaverEmployee - t.AccLevy;

        return new Payslip
        {
            EmployeeId = employee.Id,
            Hours = hours,
            HourlyRate = rate,
            GrossPay = gross,
            PayeTax = t.Paye,
            StudentLoan = t.StudentLoan,
            KiwiSaverEmployee = t.KiwiSaverEmployee,
            KiwiSaverEmployer = t.KiwiSaverEmployer,
            AccLevy = t.AccLevy,
            NetPay = Math.Round(net, 2),
            TaxCode = employee.TaxCode,
        };
    }
}
