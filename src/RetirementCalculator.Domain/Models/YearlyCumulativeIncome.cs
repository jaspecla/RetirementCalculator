namespace RetirementCalculator.Domain.Models;

/// <summary>
/// A single data point in a scenario's year-by-year cumulative income series, used to plot
/// cumulative benefits received over time.
/// </summary>
/// <param name="Year">1-based count of whole years elapsed since the scenario's claim age.</param>
/// <param name="CumulativeAmount">Total nominal dollars received from claiming through this year mark.</param>
public sealed record YearlyCumulativeIncome(int Year, decimal CumulativeAmount);
