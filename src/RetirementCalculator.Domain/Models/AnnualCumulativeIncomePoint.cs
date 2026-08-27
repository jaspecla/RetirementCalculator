namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Immutable annual cumulative-income point for a single retirement-income scenario.
/// </summary>
public readonly record struct AnnualCumulativeIncomePoint(Age Age, decimal CumulativeIncome);
