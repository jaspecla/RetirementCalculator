using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Domain.Services;

/// <summary>
/// Computes a side-by-side Social Security claiming comparison between a chosen claim age
/// (62 through full retirement age) and waiting until full retirement age (FRA). All amounts
/// are constant nominal dollars (no inflation, COLA, taxes, earnings test, or other
/// adjustments are modeled). Claiming after FRA is out of scope.
/// </summary>
public static class SocialSecurityBenefitCalculator
{
    /// <summary>
    /// Upper bound (in whole years of age) used when searching for a break-even age so the
    /// search terminates even when the planning age is far short of the true break-even point.
    /// </summary>
    private const int BreakEvenSearchHorizonYears = 120;

    /// <summary>
    /// Calculates the full comparison. Caller is expected to have already validated
    /// <paramref name="input"/> with <see cref="Validation.SocialSecurityInputValidator"/>.
    /// </summary>
    public static SocialSecurityComparisonResult Calculate(SocialSecurityCalculatorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var birthYear = input.BirthYear ?? throw new ArgumentException("Birth year is required.", nameof(input));
        var fraBenefit = input.MonthlyBenefitAtFullRetirementAge
            ?? throw new ArgumentException("Monthly benefit at full retirement age is required.", nameof(input));
        var claimAge = new Age(
            input.ClaimAgeYears ?? throw new ArgumentException("Claim age years is required.", nameof(input)),
            input.ClaimAgeMonths ?? throw new ArgumentException("Claim age months is required.", nameof(input)));
        var planningAge = new Age(
            input.PlanningAgeYears ?? throw new ArgumentException("Planning age years is required.", nameof(input)),
            input.PlanningAgeMonths ?? throw new ArgumentException("Planning age months is required.", nameof(input)));

        var fullRetirementAge = FullRetirementAgeCalculator.Calculate(birthYear);

        var monthsEarly = Math.Max(fullRetirementAge.TotalMonths - claimAge.TotalMonths, 0);
        var reductionFraction = EarlyClaimingReductionCalculator.CalculateReductionFraction(monthsEarly);
        var chosenAgeMonthlyBenefit = Math.Round(fraBenefit * (1m - reductionFraction), 2, MidpointRounding.AwayFromZero);

        var isChosenAgeSameAsFra = claimAge.TotalMonths == fullRetirementAge.TotalMonths;
        var earliestClaimAge = Age.FromTotalMonths(Math.Min(claimAge.TotalMonths, fullRetirementAge.TotalMonths));

        var chosenAgeScenario = new ScenarioResult
        {
            ClaimAge = claimAge,
            PlanningAge = planningAge,
            MonthlyBenefit = chosenAgeMonthlyBenefit,
            PaymentMonthsThroughPlanningAge = PaymentMonths(claimAge, planningAge),
            AnnualCumulativeIncome = BuildAnnualCumulativeIncomePoints(earliestClaimAge, planningAge, claimAge, chosenAgeMonthlyBenefit),
        };

        var fraScenario = new ScenarioResult
        {
            ClaimAge = fullRetirementAge,
            PlanningAge = planningAge,
            MonthlyBenefit = fraBenefit,
            PaymentMonthsThroughPlanningAge = PaymentMonths(fullRetirementAge, planningAge),
            AnnualCumulativeIncome = BuildAnnualCumulativeIncomePoints(earliestClaimAge, planningAge, fullRetirementAge, fraBenefit),
        };

        Age? breakEvenAge = isChosenAgeSameAsFra
            ? null
            : FindBreakEvenAge(claimAge, chosenAgeMonthlyBenefit, fullRetirementAge, fraBenefit);

        return new SocialSecurityComparisonResult
        {
            FullRetirementAge = fullRetirementAge,
            ChosenAgeScenario = chosenAgeScenario,
            FullRetirementAgeScenario = fraScenario,
            IsChosenAgeSameAsFullRetirementAge = isChosenAgeSameAsFra,
            BreakEvenAge = breakEvenAge,
        };
    }

    private static int PaymentMonths(Age claimAge, Age planningAge) =>
        Math.Max(planningAge.TotalMonths - claimAge.TotalMonths, 0);

    private static decimal CumulativeAt(int atTotalMonths, Age claimAge, decimal monthlyBenefit) =>
        monthlyBenefit * Math.Max(atTotalMonths - claimAge.TotalMonths, 0);

    /// <summary>
    /// Builds an ordered annual cumulative-income series that begins at the earliest claim age
    /// (or exact planning age when the two are the same), includes every annual milestone, and
    /// appends the exact planning-age endpoint when it falls between milestone years.
    /// </summary>
    private static IReadOnlyList<AnnualCumulativeIncomePoint> BuildAnnualCumulativeIncomePoints(
        Age projectionStartAge,
        Age planningAge,
        Age claimAge,
        decimal monthlyBenefit)
    {
        var totalMonthsSet = new SortedSet<int> { projectionStartAge.TotalMonths };

        var nextAnnualMilestone = (projectionStartAge.TotalMonths / 12) * 12 + 12;
        while (nextAnnualMilestone <= planningAge.TotalMonths)
        {
            totalMonthsSet.Add(nextAnnualMilestone);
            nextAnnualMilestone += 12;
        }

        totalMonthsSet.Add(planningAge.TotalMonths);

        var points = new List<AnnualCumulativeIncomePoint>(totalMonthsSet.Count);
        foreach (var totalMonths in totalMonthsSet)
        {
            var age = Age.FromTotalMonths(totalMonths);
            var cumulativeIncome = CumulativeAt(totalMonths, claimAge, monthlyBenefit);
            points.Add(new AnnualCumulativeIncomePoint(age, cumulativeIncome));
        }

        return points;
    }

    /// <summary>
    /// Searches month-by-month, starting at full retirement age, for the first age at which
    /// cumulative FRA-scenario dollars catch up to and surpass the chosen-age scenario's
    /// cumulative dollars. The search is independent of the planning age so a break-even
    /// beyond the user's planning horizon is still found and reported. Returns null if no
    /// break-even occurs within <see cref="BreakEvenSearchHorizonYears"/>.
    /// </summary>
    private static Age? FindBreakEvenAge(Age chosenAge, decimal chosenMonthlyBenefit, Age fraAge, decimal fraMonthlyBenefit)
    {
        var horizonTotalMonths = BreakEvenSearchHorizonYears * 12;

        for (var totalMonths = fraAge.TotalMonths; totalMonths <= horizonTotalMonths; totalMonths++)
        {
            var chosenCumulative = CumulativeAt(totalMonths, chosenAge, chosenMonthlyBenefit);
            var fraCumulative = CumulativeAt(totalMonths, fraAge, fraMonthlyBenefit);

            if (fraCumulative >= chosenCumulative)
            {
                return Age.FromTotalMonths(totalMonths);
            }
        }

        return null;
    }
}
