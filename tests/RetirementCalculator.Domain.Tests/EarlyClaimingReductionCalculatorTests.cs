using RetirementCalculator.Domain.Services;

namespace RetirementCalculator.Domain.Tests;

[TestClass]
public sealed class EarlyClaimingReductionCalculatorTests
{
    [TestMethod]
    public void CalculateReductionFraction_ZeroMonthsEarly_ReturnsZero()
    {
        var reduction = EarlyClaimingReductionCalculator.CalculateReductionFraction(0);

        Assert.AreEqual(0m, reduction);
    }

    [TestMethod]
    public void CalculateReductionFraction_ExactlyThirtySixMonths_UsesOnlyFirstTierRate()
    {
        // 36 months * 5/9 of 1% = 20%
        var reduction = EarlyClaimingReductionCalculator.CalculateReductionFraction(36);

        Assert.AreEqual(0.20m, Math.Round(reduction, 6));
    }

    [TestMethod]
    public void CalculateReductionFraction_OneMonthPastThirtySix_AddsSecondTierRate()
    {
        // 20% (first 36 months) + 1 * 5/12 of 1% (~0.4167%)
        var reduction = EarlyClaimingReductionCalculator.CalculateReductionFraction(37);
        var expected = 0.20m + (5m / 12m / 100m);

        Assert.AreEqual(Math.Round(expected, 8), Math.Round(reduction, 8));
        Assert.IsTrue(reduction > 0.20m, "Reduction should exceed the 36-month plateau once past the boundary.");
    }

    [TestMethod]
    public void CalculateReductionFraction_ThirtyFiveMonths_IsLessThanThirtySixMonthReduction()
    {
        var reductionAt35 = EarlyClaimingReductionCalculator.CalculateReductionFraction(35);
        var reductionAt36 = EarlyClaimingReductionCalculator.CalculateReductionFraction(36);

        Assert.IsTrue(reductionAt35 < reductionAt36);
    }

    [TestMethod]
    public void CalculateReductionFraction_AgeSixtyTwoWithFraSixtySeven_MatchesKnownSsaValue()
    {
        // Claiming at 62 with FRA 67 is 60 months early -> known SSA reduction is 30% (70% of PIA).
        var reduction = EarlyClaimingReductionCalculator.CalculateReductionFraction(60);

        Assert.AreEqual(0.30m, Math.Round(reduction, 6));
    }

    [TestMethod]
    public void CalculateReductionFraction_AgeSixtyTwoWithFraSixtySix_MatchesKnownSsaValue()
    {
        // Claiming at 62 with FRA 66 is 48 months early -> known SSA reduction is 25% (75% of PIA).
        var reduction = EarlyClaimingReductionCalculator.CalculateReductionFraction(48);

        Assert.AreEqual(0.25m, Math.Round(reduction, 6));
    }

    [TestMethod]
    public void CalculateReductionFraction_NegativeMonths_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            EarlyClaimingReductionCalculator.CalculateReductionFraction(-1));
    }
}
