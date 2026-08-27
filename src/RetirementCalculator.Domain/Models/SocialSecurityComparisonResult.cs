namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Full side-by-side comparison between claiming at a chosen (early or FRA) age versus
/// waiting until full retirement age (FRA). When the chosen claim age equals FRA the two
/// scenarios are identical and no meaningful break-even exists.
/// </summary>
public sealed class SocialSecurityComparisonResult
{
    public required Age FullRetirementAge { get; init; }

    /// <summary>Scenario for the age the user chose to compare (may equal FRA).</summary>
    public required ScenarioResult ChosenAgeScenario { get; init; }

    /// <summary>Scenario for waiting until full retirement age.</summary>
    public required ScenarioResult FullRetirementAgeScenario { get; init; }

    /// <summary>Ordered cumulative-income points from the claim age through the planning age.</summary>
    public IReadOnlyList<CumulativeIncomeProjectionPoint> CumulativeIncomeProjection { get; init; } = Array.Empty<CumulativeIncomeProjectionPoint>();

    /// <summary>Alias for <see cref="CumulativeIncomeProjection"/> used by graphing consumers.</summary>
    public IReadOnlyList<AnnualCumulativeIncomeProjectionPoint> AnnualCumulativeIncomeProjection =>
        CumulativeIncomeProjection is IReadOnlyList<AnnualCumulativeIncomeProjectionPoint> annualProjection
            ? annualProjection
            : Array.Empty<AnnualCumulativeIncomeProjectionPoint>();

    /// <summary>Alias for <see cref="CumulativeIncomeProjection"/>.</summary>
    public IReadOnlyList<CumulativeIncomeProjectionPoint> ProjectionPoints => CumulativeIncomeProjection;

    /// <summary>Alias for <see cref="CumulativeIncomeProjection"/>.</summary>
    public IReadOnlyList<CumulativeIncomeProjectionPoint> CumulativeIncomeProjectionPoints => CumulativeIncomeProjection;

    /// <summary>Alias for <see cref="AnnualCumulativeIncomeProjection"/>.</summary>
    public IReadOnlyList<AnnualCumulativeIncomeProjectionPoint> AnnualProjectionPoints => AnnualCumulativeIncomeProjection;

    /// <summary>True when the chosen claim age is the same as FRA (identical scenarios).</summary>
    public required bool IsChosenAgeSameAsFullRetirementAge { get; init; }

    /// <summary>Dollar increase in monthly benefit gained by waiting from the chosen age to FRA.</summary>
    public decimal WaitingIncreaseAmount => FullRetirementAgeScenario.MonthlyBenefit - ChosenAgeScenario.MonthlyBenefit;

    /// <summary>Percentage increase in monthly benefit gained by waiting from the chosen age to FRA.</summary>
    public decimal WaitingIncreasePercent => ChosenAgeScenario.MonthlyBenefit == 0m
        ? 0m
        : WaitingIncreaseAmount / ChosenAgeScenario.MonthlyBenefit * 100m;

    /// <summary>
    /// Cumulative dollars received under the FRA scenario minus the chosen-age scenario,
    /// both measured through the planning age. Positive means waiting for FRA is ahead.
    /// </summary>
    public decimal CumulativeDifferenceAtPlanningAge =>
        FullRetirementAgeScenario.CumulativeTotalThroughPlanningAge - ChosenAgeScenario.CumulativeTotalThroughPlanningAge;

    /// <summary>
    /// The first age at which cumulative FRA-scenario dollars catch up to and surpass the
    /// chosen-age scenario's cumulative dollars, even if that age is beyond the requested
    /// planning age. Null when the chosen age already equals FRA (no meaningful break-even)
    /// or when no break-even occurs within the search horizon.
    /// </summary>
    public Age? BreakEvenAge { get; init; }
}
