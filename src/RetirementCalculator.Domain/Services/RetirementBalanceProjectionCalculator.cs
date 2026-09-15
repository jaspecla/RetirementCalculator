using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Domain.Services;

/// <summary>
/// Projects annual expenditure and account balances from retirement through planning age.
/// </summary>
public static class RetirementBalanceProjectionCalculator
{
    public static RetirementBalanceProjectionResult Calculate(
        SocialSecurityCalculatorInput input,
        ScenarioResult scenario,
        InflationPathResult? inflationPath = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(scenario);

        var retirementAge = input.RetirementAgeYears is int retirementYears && input.RetirementAgeMonths is int retirementMonths
            ? new Age(retirementYears, retirementMonths)
            : scenario.ClaimAge;

        var planningAge = input.PlanningAgeYears is int planningYears && input.PlanningAgeMonths is int planningMonths
            ? new Age(planningYears, planningMonths)
            : scenario.ProjectionSeries[^1].Age;

        var averageInflationRate = input.AverageInflationRate ?? 2.5m;
        var currentYear = input.CurrentYear ?? DateTime.UtcNow.Year;
        var currentAge = input.BirthYear is int birthYear ? new Age(currentYear - birthYear, 0) : retirementAge;
        var annualInflationRates = inflationPath is null
            ? InflationPathCalculator.Calculate(averageInflationRate, Math.Max(1, ((planningAge.TotalMonths - currentAge.TotalMonths) / 12) + 1)).AnnualRates
            : inflationPath.AnnualRates;

        return CalculateCore(
            input.InitialAccountBalance ?? 0m,
            input.MonthlySpendingInTodaysDollars ?? 0m,
            retirementAge,
            planningAge,
            annualInflationRates,
            currentAge,
            currentYear,
            yearOffset => scenario.GetAnnualBenefitForYearOffset(yearOffset));
    }

    public static RetirementBalanceProjectionResult Calculate(
        decimal initialAccountBalance,
        decimal monthlySpendingInTodaysDollars,
        Age retirementAge,
        Age planningAge,
        IReadOnlyList<decimal> annualInflationRates,
        Age claimAge,
        decimal monthlyBenefit,
        int? currentYear = null)
    {
        var rates = annualInflationRates is null || annualInflationRates.Count == 0
            ? new[] { 0m }
            : annualInflationRates.ToArray();

        var timelineStartAge = retirementAge;
        var anchorYear = currentYear ?? retirementAge.TotalYears;

        return CalculateCore(
            initialAccountBalance,
            monthlySpendingInTodaysDollars,
            retirementAge,
            planningAge,
            rates,
            timelineStartAge,
            anchorYear,
            yearOffset => CalculateAnnualSocialSecurityIncome(claimAge, monthlyBenefit, yearOffset, retirementAge, rates));
    }

    private static RetirementBalanceProjectionResult CalculateCore(
        decimal initialAccountBalance,
        decimal monthlySpendingInTodaysDollars,
        Age retirementAge,
        Age planningAge,
        IReadOnlyList<decimal> annualInflationRates,
        Age timelineStartAge,
        int? anchorYear,
        Func<int, decimal> getAnnualSocialSecurityIncome)
    {
        var rates = annualInflationRates is null || annualInflationRates.Count == 0
            ? new[] { 0m }
            : annualInflationRates.ToArray();

        var bars = new List<RetirementBalanceProjectionPoint>();
        var balance = initialAccountBalance;
        decimal firstYearNetWithdrawal = 0m;
        bool isFirstYearAboveGuideline = false;
        Age? firstDepletionAge = null;

        var firstProjectionYearOffset = Math.Max(0, (retirementAge.TotalMonths - timelineStartAge.TotalMonths) / 12);
        var projectionYears = Math.Max(0, (planningAge.TotalMonths - retirementAge.TotalMonths) / 12 + 1);

        for (var yearOffset = 0; yearOffset < projectionYears; yearOffset++)
        {
            var timelineYearOffset = firstProjectionYearOffset + yearOffset;
            var age = Age.FromTotalMonths(timelineStartAge.TotalMonths + (timelineYearOffset * 12));
            if (age.TotalMonths > planningAge.TotalMonths)
            {
                break;
            }

            var annualExpense = monthlySpendingInTodaysDollars * 12m;
            for (var compoundIndex = 0; compoundIndex <= timelineYearOffset; compoundIndex++)
            {
                var rate = rates[Math.Min(compoundIndex, rates.Length - 1)] / 100m;
                annualExpense *= 1m + rate;
            }

            var annualSocialSecurityIncome = getAnnualSocialSecurityIncome(timelineYearOffset);
            var netWithdrawal = Math.Max(annualExpense - annualSocialSecurityIncome, 0m);
            var beginningBalance = balance;
            balance = Math.Max(0m, balance - netWithdrawal);

            if (yearOffset == 0)
            {
                firstYearNetWithdrawal = netWithdrawal;
                if (initialAccountBalance == 0m)
                {
                    isFirstYearAboveGuideline = netWithdrawal > 0m;
                }
                else
                {
                    isFirstYearAboveGuideline = netWithdrawal > initialAccountBalance * 0.04m;
                }
            }

            if (firstDepletionAge is null && netWithdrawal > 0m && balance == 0m)
            {
                firstDepletionAge = age;
            }

            bars.Add(new RetirementBalanceProjectionPoint(
                timelineYearOffset,
                age,
                beginningBalance,
                annualExpense,
                annualSocialSecurityIncome,
                netWithdrawal,
                balance));
        }

        return new RetirementBalanceProjectionResult
        {
            RetirementAge = retirementAge,
            PlanningAge = planningAge,
            InitialAccountBalance = initialAccountBalance,
            ProjectionSeries = new OrderedRetirementBalanceProjectionSeries(bars),
            FirstYearNetWithdrawal = firstYearNetWithdrawal,
            GuidelineThreshold = initialAccountBalance == 0m ? 0m : initialAccountBalance * 0.04m,
            IsFirstYearNetWithdrawalAboveFourPercentGuideline = isFirstYearAboveGuideline,
            FirstDepletionAge = firstDepletionAge,
            FirstDepletionCalendarYear = DetermineDepletionCalendarYear(firstDepletionAge, timelineStartAge, anchorYear, retirementAge),
        };
    }

    private static int? DetermineDepletionCalendarYear(Age? firstDepletionAge, Age timelineStartAge, int? anchorYear, Age retirementAge)
    {
        if (firstDepletionAge is not Age depletionAge)
        {
            return null;
        }

        if (anchorYear is int year && timelineStartAge.TotalMonths >= 0)
        {
            return year + ((depletionAge.TotalMonths - timelineStartAge.TotalMonths) / 12);
        }

        return retirementAge.TotalYears + ((depletionAge.TotalMonths - retirementAge.TotalMonths) / 12);
    }

    private static decimal CalculateAnnualSocialSecurityIncome(Age claimAge, decimal monthlyBenefit, int yearOffsetFromRetirement, Age retirementAge, IReadOnlyList<decimal> annualInflationRates)
    {
        var yearsSinceClaim = yearOffsetFromRetirement + (retirementAge.TotalMonths / 12) - (claimAge.TotalMonths / 12);
        if (yearsSinceClaim < 0)
        {
            return 0m;
        }

        var adjustedMonthlyBenefit = monthlyBenefit;
        for (var i = 0; i < yearsSinceClaim; i++)
        {
            var rate = annualInflationRates.Count == 0
                ? 0m
                : annualInflationRates[Math.Min(i, annualInflationRates.Count - 1)] / 100m;
            adjustedMonthlyBenefit *= 1m + rate;
        }

        return adjustedMonthlyBenefit * 12m;
    }
}
