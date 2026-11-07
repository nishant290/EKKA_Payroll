using PayrollNZ.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayrollNZ.Web.Services;

public interface IPayslipPdfService
{
    /// <summary>The payslip must be loaded with PayRun.Company, Employee and Employee.BankDetails.</summary>
    byte[] Generate(Payslip slip);
}

public class PayslipPdfService : IPayslipPdfService
{
    private const string Brand = "#13B5EA";

    static PayslipPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;   // check QuestPDF licence terms for your company size
    }

    public byte[] Generate(Payslip s)
    {
        var emp = s.Employee!;
        var run = s.PayRun!;
        var co = run.Company!;
        var bank = emp.BankDetails.FirstOrDefault(b => b.IsPrimary) ?? emp.BankDetails.FirstOrDefault();

        string Money(decimal v) => v.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-NZ"));
        string D(DateOnly d) => d.ToString("dd MMM yyyy");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(co.TradingName ?? co.Name).FontSize(18).Bold().FontColor(Brand);
                            if (!string.IsNullOrWhiteSpace(co.IrdNumber))
                                c.Item().Text($"Employer IRD: {co.IrdNumber}").FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(150).AlignRight().Text("PAYSLIP").FontSize(22).Bold();
                    });
                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Brand);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Employee").Bold();
                            c.Item().Text(emp.FullName);
                            if (!string.IsNullOrWhiteSpace(emp.JobTitle)) c.Item().Text(emp.JobTitle);
                            c.Item().Text($"Tax code: {s.TaxCode}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Pay period").Bold();
                            c.Item().Text($"{D(run.PeriodStart)} – {D(run.PeriodEnd)}");
                            c.Item().Text($"Payment date: {D(run.PaymentDate)}");
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1.6f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Brand).Padding(5).Text("Earnings").Bold().FontColor(Colors.White);
                            h.Cell().Background(Brand).Padding(5).AlignRight().Text("Hours").Bold().FontColor(Colors.White);
                            h.Cell().Background(Brand).Padding(5).AlignRight().Text("Rate").Bold().FontColor(Colors.White);
                            h.Cell().Background(Brand).Padding(5).AlignRight().Text("Amount").Bold().FontColor(Colors.White);
                        });

                        var isSalary = emp.PayType == PayType.Salary;
                        table.Cell().Padding(5).Text(isSalary ? "Salary" : "Ordinary hours");
                        table.Cell().Padding(5).AlignRight().Text(isSalary ? "-" : s.Hours.ToString("0.00"));
                        table.Cell().Padding(5).AlignRight().Text(isSalary ? "-" : Money(s.HourlyRate));
                        table.Cell().Padding(5).AlignRight().Text(Money(s.GrossPay));
                    });

                    col.Item().AlignRight().Width(260).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(90); });

                        void Line(string label, decimal amount, bool bold = false)
                        {
                            table.Cell().DefaultTextStyle(x => bold ? x.Bold() : x).Padding(3).Text(label);
                            table.Cell().DefaultTextStyle(x => bold ? x.Bold() : x).Padding(3).AlignRight().Text(Money(amount));
                        }

                        Line("Gross pay", s.GrossPay, true);
                        Line("PAYE", -s.PayeTax);
                        if (s.StudentLoan != 0) Line("Student loan", -s.StudentLoan);
                        if (s.KiwiSaverEmployee != 0) Line("KiwiSaver (employee)", -s.KiwiSaverEmployee);
                        if (s.AccLevy != 0) Line("ACC earners' levy", -s.AccLevy);
                        Line("Net pay", s.NetPay, true);
                    });

                    if (s.KiwiSaverEmployer != 0)
                    {
                        col.Item().Text($"Employer KiwiSaver contribution: {Money(s.KiwiSaverEmployer)}")
                            .FontColor(Colors.Grey.Darken1);
                    }

                    if (bank != null)
                    {
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text("Paid to").Bold();
                            c.Item().Text($"{bank.AccountName} · {bank.BankName} · {MaskAccount(bank.AccountNumber)}");
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated by PayrollNZ · ").FontColor(Colors.Grey.Medium);
                    t.Span("Please keep this payslip for your records.").FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }

    /// <summary>Show only the last digits, e.g. **-****-*******-00.</summary>
    private static string MaskAccount(string account)
    {
        var parts = account.Split('-');
        if (parts.Length < 4) return account;
        return $"**-****-*****{parts[2][^2..]}-{parts[3]}";
    }
}
