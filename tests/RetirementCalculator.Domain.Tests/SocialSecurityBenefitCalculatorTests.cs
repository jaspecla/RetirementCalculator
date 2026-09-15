using RetirementCalculator.Domain.Models;
using RetirementCalculator.Domain.Services;

namespace RetirementCalculator.Domain.Tests;

[TestClass]
public sealed class SocialSecurityBenefitCalculatorTests
{
    private static SocialSecurityCalculatorInput CreateInput(
        int birthYear,
        decimal fraBenefit,
        int claimYears,
        int claimMonths,
        int planningYears,
        int planningMonths = 0,
        int retirementAgeYears = 67,
        int retirementAgeMonths = 0,
        decimal averageInflationRate = 2.5m,
        decimal initialBalance = 100_000m,
        decimal monthlySpending = 3_000m,
        int? currentYear = null) => new()
        {
            BirthYear = birthYear,
            CurrentYear = currentYear,
            MonthlyBenefitAtFullRetirementAge = fraBenefit,
            InitialAccountBalance = initialBalance,
            MonthlySpendingInTodaysDollars = monthlySpending,
            RetirementAgeYears = retirementAgeYears,
            RetirementAgeMonths = retirementAgeMonths,
            AverageInflationRate = averageInflationRate,
            ClaimAgeYears = claimYears,
            ClaimAgeMonths = claimMonths,
            PlanningAgeYears = planningYears,
            PlanningAgeMonths = planningMonths,
        };

    [TestMethod]
    public void InflationPathCalculator_GeneratesAverageFirstRateAndHysteresisAndClampsToBoundaries()
    {
        var changes = new[] { 1.5m, 1.5m, -1.5m, 1.5m };
        var index = 0;
        var path = InflationPathCalculator.Calculate(2.5m, changes.Length + 1, () => changes[index++]);

        Assert.AreEqual(2.50m, path[0]);
        Assert.AreEqual(4.00m, path[1]);
        Assert.AreEqual(5.05m, path[2]);
        Assert.AreEqual(2.785m, path[3]);
        Assert.AreEqual(4.1995m, path[4]);

        var clamped = InflationPathCalculator.Calculate(11.8m, 4, () => 1.5m);
        Assert.AreEqual(11.80m, clamped[0]);
        Assert.AreEqual(12m, clamped[1]);
        Assert.AreEqual(12m, clamped[2]);
        Assert.AreEqual(12m, clamped[3]);

        var lowClamped = InflationPathCalculator.Calculate(0.5m, 4, () => -1.5m);
        Assert.AreEqual(0.5m, lowClamped[0]);
        Assert.AreEqual(0m, lowClamped[1]);
        Assert.AreEqual(0m, lowClamped[2]);
        Assert.AreEqual(0m, lowClamped[3]);
    }

    [TestMethod]
    public void Calculate_UsesSharedPathAndClaimYearBaseBenefitWithPreclaimZeros()
    {
        var input = CreateInput(
            birthYear: 1965,
            fraBenefit: 2000m,
            claimYears: 62,
            claimMonths: 0,
            planningYears: 68,
            averageInflationRate: 2m,
            retirementAgeYears: 67,
            retirementAgeMonths: 0,
            initialBalance: 250_000m,
            monthlySpending: 4_000m,
            currentYear: 2026);

        var result = SocialSecurityBenefitCalculator.Calculate(input, () => 0m);

        Assert.AreSame(result.InflationPath, result.ChosenAgeScenario.InflationPath);
        Assert.AreSame(result.InflationPath, result.FullRetirementAgeScenario.InflationPath);
        Assert.AreEqual(0m, result.ChosenAgeScenario.AnnualBenefitSeries![0].MonthlyBenefit);
        Assert.AreEqual(0m, result.FullRetirementAgeScenario.AnnualBenefitSeries![0].MonthlyBenefit);
        Assert.AreEqual(1400m, result.ChosenAgeScenario.AnnualBenefitSeries[1].MonthlyBenefit);
        Assert.AreEqual(1400m * 12m, result.ChosenAgeScenario.AnnualBenefitSeries[1].AnnualBenefit);
        Assert.AreEqual(2000m, result.FullRetirementAgeScenario.AnnualBenefitSeries[6].MonthlyBenefit);
        Assert.AreEqual(2000m * 12m, result.FullRetirementAgeScenario.AnnualBenefitSeries[6].AnnualBenefit);
    }

    [TestMethod]
    public void RetirementBalanceProjectionCalculator_CompoundsSpendingAndOffsetsSocialSecurity()
    {
        var rates = InflationPathCalculator.Calculate(2m, 3, () => 0m).AnnualRates;
        var projection = RetirementBalanceProjectionCalculator.Calculate(
            initialAccountBalance: 50_000m,
            monthlySpendingInTodaysDollars: 2_000m,
            retirementAge: new Age(67, 0),
            planningAge: new Age(69, 0),
            annualInflationRates: rates,
            claimAge: new Age(67, 0),
            monthlyBenefit: 2_000m);

        Assert.AreEqual(24_480m, projection.ProjectionSeries[0].AnnualExpense);
        Assert.AreEqual(24_000m, projection.ProjectionSeries[0].AnnualSocialSecurityIncome);
        Assert.AreEqual(480m, projection.ProjectionSeries[0].NetWithdrawal);
        Assert.AreEqual(49_520m, projection.ProjectionSeries[0].EndingBalance);
        Assert.AreEqual(480m, projection.FirstYearNetWithdrawal);
    }

    [TestMethod]
    public void RetirementBalanceProjectionCalculator_UsesSharedSpendingPathAndScenarioOffsets()
    {
        var input = CreateInput(
            birthYear: 1965,
            fraBenefit: 2000m,
            claimYears: 62,
            claimMonths: 0,
            planningYears: 68,
            averageInflationRate: 0m,
            retirementAgeYears: 67,
            retirementAgeMonths: 0,
            initialBalance: 200_000m,
            monthlySpending: 4_000m,
            currentYear: 2026);

        var result = SocialSecurityBenefitCalculator.Calculate(input, () => 0m);
        var chosenProjection = result.ChosenAgeScenario.RetirementBalanceProjection!;
        var fraProjection = result.FullRetirementAgeScenario.RetirementBalanceProjection!;

        Assert.AreEqual(chosenProjection.ProjectionSeries[0].AnnualExpense, fraProjection.ProjectionSeries[0].AnnualExpense);
        Assert.AreEqual(result.ChosenAgeScenario.GetAnnualBenefitForYearOffset(6), chosenProjection.ProjectionSeries[0].AnnualSocialSecurityIncome);
        Assert.AreEqual(result.FullRetirementAgeScenario.GetAnnualBenefitForYearOffset(6), fraProjection.ProjectionSeries[0].AnnualSocialSecurityIncome);
        Assert.AreNotEqual(chosenProjection.ProjectionSeries[0].AnnualSocialSecurityIncome, fraProjection.ProjectionSeries[0].AnnualSocialSecurityIncome);
    }

    [TestMethod]
    public void RetirementBalanceProjectionCalculator_UsesFourPercentBoundaryWithoutWarningOnEquality()
    {
        var zeroRatePath = InflationPathCalculator.Calculate(0m, 2, () => 0m).AnnualRates;

        var equalProjection = RetirementBalanceProjectionCalculator.Calculate(
            initialAccountBalance: 60_000m,
            monthlySpendingInTodaysDollars: 200m,
            retirementAge: new Age(67, 0),
            planningAge: new Age(68, 0),
            annualInflationRates: zeroRatePath,
            claimAge: new Age(67, 0),
            monthlyBenefit: 0m);

        var aboveProjection = RetirementBalanceProjectionCalculator.Calculate(
            initialAccountBalance: 60_000m,
            monthlySpendingInTodaysDollars: 201m,
            retirementAge: new Age(67, 0),
            planningAge: new Age(68, 0),
            annualInflationRates: zeroRatePath,
            claimAge: new Age(67, 0),
            monthlyBenefit: 0m);

        var zeroBalanceProjection = RetirementBalanceProjectionCalculator.Calculate(
            initialAccountBalance: 0m,
            monthlySpendingInTodaysDollars: 100m,
            retirementAge: new Age(67, 0),
            planningAge: new Age(68, 0),
            annualInflationRates: zeroRatePath,
            claimAge: new Age(67, 0),
            monthlyBenefit: 0m);

        Assert.AreEqual(2_400m, equalProjection.FirstYearNetWithdrawal);
        Assert.IsFalse(equalProjection.IsFirstYearNetWithdrawalAboveFourPercentGuideline);
        Assert.IsTrue(aboveProjection.IsFirstYearNetWithdrawalAboveFourPercentGuideline);
        Assert.IsTrue(zeroBalanceProjection.IsFirstYearNetWithdrawalAboveFourPercentGuideline);
    }

    [TestMethod]
    public void RetirementBalanceProjectionCalculator_RecordsExactDepletionAndCalendarYear()
    {
        var input = CreateInput(
            birthYear: 1965,
            fraBenefit: 2000m,
            claimYears: 62,
            claimMonths: 0,
            planningYears: 68,
            averageInflationRate: 0m,
            retirementAgeYears: 67,
            retirementAgeMonths: 0,
            initialBalance: 5_000m,
            monthlySpending: 2_000m,
            currentYear: 2026);

        var result = SocialSecurityBenefitCalculator.Calculate(input, () => 0m);
        var projection = result.ChosenAgeScenario.RetirementBalanceProjection!;

        Assert.AreEqual(new Age(67, 0), projection.FirstDepletionAge);
        Assert.AreEqual(2032, projection.FirstDepletionCalendarYear);
        Assert.AreEqual(0m, projection.ProjectionSeries[0].EndingBalance);
    }
}
