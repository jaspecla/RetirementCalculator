namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Projected results for a single claiming age, used to build a side-by-side comparison.
/// </summary>
public sealed class ScenarioResult
{
    public required Age ClaimAge { get; init; }

    /// <summary>Constant nominal monthly benefit amount for this scenario.</summary>
    public required decimal MonthlyBenefit { get; init; }

    /// <summary>Constant nominal annual benefit amount for this scenario (monthly * 12).</summary>
    public decimal AnnualBenefit => MonthlyBenefit * 12m;

    /// <summary>Number of monthly payments received between claiming and the planning age (inclusive of the claim month, exclusive after the planning age is reached).</summary>
    public required int PaymentMonthsThroughPlanningAge { get; init; }

    /// <summary>Total nominal dollars received from claiming through the planning age.</summary>
    public decimal CumulativeTotalThroughPlanningAge => MonthlyBenefit * PaymentMonthsThroughPlanningAge;
}
