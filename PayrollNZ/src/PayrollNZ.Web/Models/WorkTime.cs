using System.ComponentModel.DataAnnotations;

namespace PayrollNZ.Web.Models;

/// <summary>Hours actually worked. These feed the pay run for hourly employees.</summary>
public class TimesheetEntry
{
    public int Id { get; set; }

    [Display(Name = "Employee")]
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Date")]
    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, Display(Name = "Start")]
    public TimeOnly StartTime { get; set; } = new TimeOnly(9, 0);

    [Required, Display(Name = "Finish")]
    public TimeOnly EndTime { get; set; } = new TimeOnly(17, 0);

    [Range(0, 600), Display(Name = "Unpaid break (min)")]
    public int BreakMinutes { get; set; } = 30;

    [StringLength(250)]
    public string? Notes { get; set; }

    /// <summary>Paid hours, calculated when the entry is saved.</summary>
    public decimal Hours { get; set; }

    /// <summary>Set once the entry has been included in a pay run.</summary>
    public int? PayRunId { get; set; }

    public static decimal CalculateHours(TimeOnly start, TimeOnly end, int breakMinutes)
    {
        var span = end - start;                       // TimeOnly subtraction wraps past midnight correctly
        var minutes = (decimal)span.TotalMinutes - breakMinutes;
        return minutes <= 0 ? 0 : Math.Round(minutes / 60m, 2);
    }
}

/// <summary>A rostered shift (the schedule). Planned, not yet worked.</summary>
public class Shift
{
    public int Id { get; set; }

    [Display(Name = "Employee")]
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Date")]
    public DateOnly ShiftDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, Display(Name = "Start")]
    public TimeOnly StartTime { get; set; } = new TimeOnly(9, 0);

    [Required, Display(Name = "Finish")]
    public TimeOnly EndTime { get; set; } = new TimeOnly(17, 0);

    [Range(0, 600), Display(Name = "Unpaid break (min)")]
    public int BreakMinutes { get; set; } = 30;

    [StringLength(250)]
    public string? Notes { get; set; }

    public decimal PlannedHours => TimesheetEntry.CalculateHours(StartTime, EndTime, BreakMinutes);
}
