namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Ordered cumulative-income point for a given age in a Social Security claiming comparison.
/// </summary>
public class CumulativeIncomeProjectionPoint
{
    public required Age Age { get; init; }

    /// <summary>
    /// Total cumulative income through this age under the chosen-age scenario.
    /// </summary>
    public required decimal ChosenAgeCumulativeIncome { get; init; }

    /// <summary>
    /// Alias for <see cref="ChosenAgeCumulativeIncome"/>.
    /// </summary>
    public decimal ChosenAgeCumulativeTotal => ChosenAgeCumulativeIncome;

    /// <summary>
    /// Total cumulative income through this age under the full-retirement-age scenario.
    /// </summary>
    public required decimal FullRetirementAgeCumulativeIncome { get; init; }

    /// <summary>
    /// Alias for <see cref="FullRetirementAgeCumulativeIncome"/>.
    /// </summary>
    public decimal FullRetirementAgeCumulativeTotal => FullRetirementAgeCumulativeIncome;
}

/// <summary>
/// Alias for the annual cumulative-income projection points used for graphing.
/// </summary>
public sealed class AnnualCumulativeIncomeProjectionPoint : CumulativeIncomeProjectionPoint
{
}
