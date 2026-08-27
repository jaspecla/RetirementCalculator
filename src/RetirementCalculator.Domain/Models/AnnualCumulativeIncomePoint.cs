namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Immutable annual cumulative-income point for a single retirement-income scenario.
/// </summary>
public readonly record struct AnnualCumulativeIncomePoint(Age Age, decimal CumulativeIncome);

/// <summary>
/// Backward-compatible alias for annual cumulative-income projection coordinates.
/// </summary>
public readonly record struct AnnualProjectionPoint(Age Age, decimal CumulativeIncome)
{
    public static implicit operator AnnualCumulativeIncomePoint(AnnualProjectionPoint point) =>
        new(point.Age, point.CumulativeIncome);

    public static implicit operator AnnualProjectionPoint(AnnualCumulativeIncomePoint point) =>
        new(point.Age, point.CumulativeIncome);
}

/// <summary>
/// General projection point used for annual cumulative-income series.
/// </summary>
public readonly record struct ProjectionPoint(Age Age, decimal CumulativeIncome)
{
    public static implicit operator AnnualCumulativeIncomePoint(ProjectionPoint point) =>
        new(point.Age, point.CumulativeIncome);

    public static implicit operator ProjectionPoint(AnnualCumulativeIncomePoint point) =>
        new(point.Age, point.CumulativeIncome);
}
