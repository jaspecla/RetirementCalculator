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
        int planningMonths = 0) => new()
        {
            BirthYear = birthYear,
            MonthlyBenefitAtFullRetirementAge = fraBenefit,
            ClaimAgeYears = claimYears,
            ClaimAgeMonths = claimMonths,
            PlanningAgeYears = planningYears,
            PlanningAgeMonths = planningMonths,
        };

    [TestMethod]
    public void Calculate_ClaimAtAgeSixtyTwoWithFraSixtySeven_ReducesBenefitByThirtyPercent()
    {
        // Birth year 1965 -> FRA 67. Claiming at 62 is 60 months early -> 30% reduction.
        var input = CreateInput(birthYear: 1965, fraBenefit: 2000m, claimYears: 62, claimMonths: 0, planningYears: 90);

        var result = SocialSecurityBenefitCalculator.Calculate(input);

        Assert.AreEqual(new Age(67, 0), result.FullRetirementAge);
        Assert.AreEqual(1400.00m, result.ChosenAgeScenario.MonthlyBenefit); // 2000 * 0.70
        Assert.AreEqual(2000.00m, result.FullRetirementAgeScenario.MonthlyBenefit);
        Assert.IsFalse(result.IsChosenAgeSameAsFullRetirementAge);
    }

    [TestMethod]
    public void Calculate_ClaimAgeEqualsFullRetirementAge_ScenariosAreIdenticalWithNoBreakEven()
    {
        var input = CreateInput(birthYear: 1960, fraBenefit: 2500m, claimYears: 67, claimMonths: 0, planningYears: 90);

        var result = SocialSecurityBenefitCalculator.Calculate(input);

        Assert.IsTrue(result.IsChosenAgeSameAsFullRetirementAge);
        Assert.AreEqual(result.FullRetirementAgeScenario.MonthlyBenefit, result.ChosenAgeScenario.MonthlyBenefit);
        Assert.AreEqual(0m, result.WaitingIncreaseAmount);
        Assert.AreEqual(0m, result.WaitingIncreasePercent);
        Assert.IsNull(result.BreakEvenAge);
    }

    [TestMethod]
    public void Calculate_PlanningAgeShortOfBreakEven_StillReportsBreakEvenBeyondHorizon()
    {
        // FRA 67, claim at 62 (30% reduction). Planning age set very close to claim age,
        // far short of the true break-even (which occurs a couple decades later).
        var input = CreateInput(birthYear: 1965, fraBenefit: 2000m, claimYears: 62, claimMonths: 0, planningYears: 63);

        var result = SocialSecurityBenefitCalculator.Calculate(input);

        Assert.IsNotNull(result.BreakEvenAge, "Break-even should be found even when beyond the planning horizon.");
        Assert.IsTrue(result.BreakEvenAge!.Value.TotalMonths > new Age(63, 0).TotalMonths,
            "Break-even age should be after the (too-short) planning age.");
    }

    [TestMethod]
    public void Calculate_BreakEvenAge_CumulativeAmountsAreConsistentAtThatAge()
    {
        var input = CreateInput(birthYear: 1965, fraBenefit: 2000m, claimYears: 62, claimMonths: 0, planningYears: 90);

        var result = SocialSecurityBenefitCalculator.Calculate(input);

        Assert.IsNotNull(result.BreakEvenAge);
        var breakEvenTotalMonths = result.BreakEvenAge!.Value.TotalMonths;

        var chosenMonthsPaid = Math.Max(breakEvenTotalMonths - result.ChosenAgeScenario.ClaimAge.TotalMonths, 0);
        var fraMonthsPaid = Math.Max(breakEvenTotalMonths - result.FullRetirementAgeScenario.ClaimAge.TotalMonths, 0);

        var chosenCumulativeAtBreakEven = result.ChosenAgeScenario.MonthlyBenefit * chosenMonthsPaid;
        var fraCumulativeAtBreakEven = result.FullRetirementAgeScenario.MonthlyBenefit * fraMonthsPaid;

        Assert.IsTrue(fraCumulativeAtBreakEven >= chosenCumulativeAtBreakEven,
            "At the reported break-even age, the FRA scenario's cumulative total should have caught up.");

        // One month earlier, the FRA scenario should not yet have caught up (true first break-even).
        var oneMonthEarlier = breakEvenTotalMonths - 1;
        var chosenMonthsPaidEarlier = Math.Max(oneMonthEarlier - result.ChosenAgeScenario.ClaimAge.TotalMonths, 0);
        var fraMonthsPaidEarlier = Math.Max(oneMonthEarlier - result.FullRetirementAgeScenario.ClaimAge.TotalMonths, 0);
        var chosenCumulativeEarlier = result.ChosenAgeScenario.MonthlyBenefit * chosenMonthsPaidEarlier;
        var fraCumulativeEarlier = result.FullRetirementAgeScenario.MonthlyBenefit * fraMonthsPaidEarlier;

        Assert.IsTrue(fraCumulativeEarlier < chosenCumulativeEarlier,
            "The month before break-even, the FRA scenario should still be behind.");
    }

    [TestMethod]
    public void Calculate_PaymentMonthsAndCumulativeTotals_AreInternallyConsistent()
    {
        var input = CreateInput(birthYear: 1960, fraBenefit: 1800m, claimYears: 64, claimMonths: 0, planningYears: 85);

        var result = SocialSecurityBenefitCalculator.Calculate(input);

        Assert.AreEqual(
            result.ChosenAgeScenario.MonthlyBenefit * result.ChosenAgeScenario.PaymentMonthsThroughPlanningAge,
            result.ChosenAgeScenario.CumulativeTotalThroughPlanningAge);
        Assert.AreEqual(
            result.FullRetirementAgeScenario.MonthlyBenefit * result.FullRetirementAgeScenario.PaymentMonthsThroughPlanningAge,
            result.FullRetirementAgeScenario.CumulativeTotalThroughPlanningAge);
        Assert.AreEqual(result.ChosenAgeScenario.MonthlyBenefit * 12m, result.ChosenAgeScenario.AnnualBenefit);
    }

    [TestMethod]
    public void Calculate_WaitingIncrease_IsPositiveWhenClaimingEarly()
    {
        var input = CreateInput(birthYear: 1962, fraBenefit: 2200m, claimYears: 63, claimMonths: 6, planningYears: 90);

        var result = SocialSecurityBenefitCalculator.Calculate(input);

        Assert.IsTrue(result.WaitingIncreaseAmount > 0m);
        Assert.IsTrue(result.WaitingIncreasePercent > 0m);
    }
}
