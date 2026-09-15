using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Domain.Services;

/// <summary>
/// Computes a side-by-side Social Security claiming comparison between a chosen claim age
/// and full retirement age (FRA). The comparison uses the generated inflation path for both
/// claim scenarios and includes annual account offsets from retirement through the planning age.
/// </summary>
public static class SocialSecurityBenefitCalculator
{
    private const int BreakEvenSearchHorizonYears = 120;

    public static SocialSecurityComparisonResult Calculate(SocialSecurityCalculatorInput input, Random? randomSource = null)
        => CalculateCore(input, randomSource, null);

    public static SocialSecurityComparisonResult Calculate(SocialSecurityCalculatorInput input, Func<decimal> randomChangeProvider)
        => CalculateCore(input, null, randomChangeProvider);

    private static SocialSecurityComparisonResult CalculateCore(
        SocialSecurityCalculatorInput input,
        Random? randomSource,
        Func<decimal>? randomChangeProvider)
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
        var retirementAge = input.RetirementAgeYears is int retirementYears && input.RetirementAgeMonths is int retirementMonths
            ? new Age(retirementYears, retirementMonths)
            : claimAge;

        var fullRetirementAge = FullRetirementAgeCalculator.Calculate(birthYear);
        var currentYear = input.CurrentYear ?? DateTime.UtcNow.Year;
        var currentAge = new Age(currentYear - birthYear, 0);
        var monthsEarly = Math.Max(fullRetirementAge.TotalMonths - claimAge.TotalMonths, 0);
        var reductionFraction = EarlyClaimingReductionCalculator.CalculateReductionFraction(monthsEarly);
        var chosenAgeMonthlyBenefit = Math.Round(fraBenefit * (1m - reductionFraction), 2, MidpointRounding.AwayFromZero);
        var pathLength = Math.Max(1, ((planningAge.TotalMonths - currentAge.TotalMonths) / 12) + 1);
        var averageInflation = input.AverageInflationRate ?? 2.5m;
        var inflationPath = randomChangeProvider is not null
            ? InflationPathCalculator.Calculate(averageInflation, pathLength, randomChangeProvider)
            : randomSource is null
                ? InflationPathCalculator.Calculate(averageInflation, pathLength)
                : InflationPathCalculator.Calculate(averageInflation, pathLength, randomSource);

        var chosenAgeScenario = BuildScenario(
            claimAge,
            chosenAgeMonthlyBenefit,
            planningAge,
            inflationPath,
            input,
            retirementAge,
            currentAge);

        var fraScenario = BuildScenario(
            fullRetirementAge,
            fraBenefit,
            planningAge,
            inflationPath,
            input,
            retirementAge,
            currentAge);

        var isChosenAgeSameAsFra = claimAge.TotalMonths == fullRetirementAge.TotalMonths;
        var breakEvenAge = isChosenAgeSameAsFra
            ? null
            : FindBreakEvenAge(chosenAgeScenario, fraScenario);

        var result = new SocialSecurityComparisonResult
        {
            FullRetirementAge = fullRetirementAge,
            ChosenAgeScenario = chosenAgeScenario,
            FullRetirementAgeScenario = fraScenario,
            IsChosenAgeSameAsFullRetirementAge = isChosenAgeSameAsFra,
            BreakEvenAge = breakEvenAge,
            InflationPath = inflationPath,
            RetirementAge = retirementAge,
        };

        result.RetirementBalanceProjection = RetirementBalanceProjectionCalculator.Calculate(input, chosenAgeScenario, inflationPath);
        return result;
    }

    private static ScenarioResult BuildScenario(
        Age claimAge,
        decimal monthlyBenefit,
        Age planningAge,
        InflationPathResult inflationPath,
        SocialSecurityCalculatorInput input,
        Age retirementAge,
        Age currentAge)
    {
        var annualBenefitSeries = BuildAnnualBenefitSeries(claimAge, monthlyBenefit, inflationPath, planningAge, currentAge);
        var cumulativePoints = new List<ProjectionPoint>();
        decimal cumulativeTotal = 0m;

        foreach (var annualBenefitPoint in annualBenefitSeries)
        {
            cumulativeTotal += annualBenefitPoint.AnnualBenefit;
            cumulativePoints.Add(new ProjectionPoint(annualBenefitPoint.Age, cumulativeTotal));
        }

        if (cumulativePoints.Count == 0)
        {
            cumulativePoints.Add(new ProjectionPoint(claimAge, 0m));
        }

        var scenario = new ScenarioResult
        {
            ClaimAge = claimAge,
            MonthlyBenefit = monthlyBenefit,
            PaymentMonthsThroughPlanningAge = PaymentMonths(claimAge, planningAge),
            ProjectionSeries = new OrderedCumulativeSeries(cumulativePoints),
            InflationPath = inflationPath,
            AnnualBenefitSeries = annualBenefitSeries,
        };

        scenario.RetirementBalanceProjection = RetirementBalanceProjectionCalculator.Calculate(input, scenario, inflationPath);
        return scenario;
    }

    private static OrderedAnnualBenefitSeries BuildAnnualBenefitSeries(
        Age claimAge,
        decimal monthlyBenefit,
        InflationPathResult inflationPath,
        Age planningAge,
        Age currentAge)
    {
        var points = new List<AnnualBenefitProjectionPoint>();
        var claimYearOffset = Math.Max(0, (claimAge.TotalMonths - currentAge.TotalMonths) / 12);
        var finalYearOffset = Math.Max(0, (planningAge.TotalMonths - currentAge.TotalMonths) / 12);

        for (var yearOffset = 0; yearOffset <= finalYearOffset; yearOffset++)
        {
            var ageAtYear = Age.FromTotalMonths(currentAge.TotalMonths + (yearOffset * 12));
            if (ageAtYear.TotalMonths > planningAge.TotalMonths)
            {
                break;
            }

            if (yearOffset < claimYearOffset)
            {
                points.Add(new AnnualBenefitProjectionPoint(ageAtYear, 0m, 0m, yearOffset));
                continue;
            }

            if (yearOffset == claimYearOffset)
            {
                var annualBenefit = monthlyBenefit * 12m;
                points.Add(new AnnualBenefitProjectionPoint(claimAge, monthlyBenefit, annualBenefit, yearOffset));
                continue;
            }

            var priorMonthlyBenefit = points[^1].MonthlyBenefit;
            var rate = inflationPath.Rates.Count > yearOffset
                ? inflationPath.Rates[yearOffset].Rate / 100m
                : 0m;
            var adjustedMonthlyBenefit = priorMonthlyBenefit * (1m + rate);
            points.Add(new AnnualBenefitProjectionPoint(ageAtYear, adjustedMonthlyBenefit, adjustedMonthlyBenefit * 12m, yearOffset));
        }

        return points.Count == 0
            ? new OrderedAnnualBenefitSeries(new[] { new AnnualBenefitProjectionPoint(claimAge, monthlyBenefit, monthlyBenefit * 12m, claimYearOffset) })
            : new OrderedAnnualBenefitSeries(points);
    }

    private static int PaymentMonths(Age claimAge, Age planningAge) =>
        Math.Max(planningAge.TotalMonths - claimAge.TotalMonths, 0);

    private static Age? FindBreakEvenAge(ScenarioResult chosenScenario, ScenarioResult fraScenario)
    {
        var startMonths = Math.Max(chosenScenario.ClaimAge.TotalMonths, fraScenario.ClaimAge.TotalMonths);
        var horizonTotalMonths = BreakEvenSearchHorizonYears * 12;

        for (var totalMonths = startMonths; totalMonths <= horizonTotalMonths; totalMonths += 12)
        {
            var age = Age.FromTotalMonths(totalMonths);
            var chosenCumulative = chosenScenario.GetCumulativeTotalAt(age);
            var fraCumulative = fraScenario.GetCumulativeTotalAt(age);

            if (fraCumulative >= chosenCumulative)
            {
                return age;
            }
        }

        return null;
    }
}
