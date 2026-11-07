using PayrollNZ.Web.Models;

namespace PayrollNZ.Web.Services;

/// <summary>
/// Seam for the NZ tax engine. PHASE 2 replaces <see cref="BasicTaxCalculator"/> with the full
/// implementation (all tax codes, secondary tables, student loan, KiwiSaver ESCT, ACC earners' levy).
/// </summary>
public interface ITaxCalculator
{
    TaxResult Calculate(Employee employee, decimal grossPay, PayFrequency frequency);
}

public record TaxResult(
    decimal Paye,
    decimal StudentLoan,
    decimal KiwiSaverEmployee,
    decimal KiwiSaverEmployer,
    decimal AccLevy);

/// <summary>
/// PLACEHOLDER ONLY. Annualises pay and applies resident brackets / flat secondary rates.
/// It is NOT IRD-accurate (no ACC, no student loan, no ESCT, no IRD PAYE table rounding)
/// and must be verified/replaced in phase 2 before real payroll is run.
/// </summary>
public class BasicTaxCalculator : ITaxCalculator
{
    // Bracket upper limits and rates for primary (M) income. VERIFY against current IRD figures.
    private static readonly (decimal UpTo, decimal Rate)[] Brackets =
    {
        (15_600m, 0.105m),
        (53_500m, 0.175m),
        (78_100m, 0.30m),
        (180_000m, 0.33m),
        (decimal.MaxValue, 0.39m),
    };

    // Flat secondary-income rates by code. VERIFY against current IRD figures.
    private static readonly Dictionary<string, decimal> SecondaryRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SB"] = 0.105m,
        ["S"] = 0.175m,
        ["SH"] = 0.30m,
        ["ST"] = 0.33m,
        ["SA"] = 0.39m,
        ["ND"] = 0.45m,
    };

    public static int PeriodsPerYear(PayFrequency f) => f switch
    {
        PayFrequency.Weekly => 52,
        PayFrequency.Fortnightly => 26,
        _ => 12,
    };

    public TaxResult Calculate(Employee employee, decimal grossPay, PayFrequency frequency)
    {
        var periods = PeriodsPerYear(frequency);
        var code = (employee.TaxCode ?? "M").Trim().ToUpperInvariant();

        decimal paye;
        if (SecondaryRates.TryGetValue(code, out var flat))
        {
            paye = grossPay * flat;
        }
        else
        {
            var annual = grossPay * periods;
            paye = AnnualTax(annual) / periods;
        }

        var ksEmployee = grossPay * employee.KiwiSaverEmployeeRate / 100m;
        var ksEmployer = grossPay * employee.KiwiSaverEmployerRate / 100m;   // ESCT comes in phase 2

        return new TaxResult(
            Paye: Math.Round(paye, 2),
            StudentLoan: 0m,                        // phase 2
            KiwiSaverEmployee: Math.Round(ksEmployee, 2),
            KiwiSaverEmployer: Math.Round(ksEmployer, 2),
            AccLevy: 0m);                           // phase 2
    }

    private static decimal AnnualTax(decimal annualIncome)
    {
        decimal tax = 0m, lower = 0m;
        foreach (var (upTo, rate) in Brackets)
        {
            if (annualIncome <= lower) break;
            var taxable = Math.Min(annualIncome, upTo) - lower;
            tax += taxable * rate;
            lower = upTo;
        }
        return tax;
    }
}
