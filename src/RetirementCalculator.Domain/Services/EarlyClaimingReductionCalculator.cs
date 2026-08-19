namespace RetirementCalculator.Domain.Services;

/// <summary>
/// Computes the early-claiming reduction applied to the full retirement age (FRA) benefit
/// when a worker claims before FRA. Does not handle claiming after FRA (out of scope).
/// </summary>
public static class EarlyClaimingReductionCalculator
{
    /// <summary>5/9 of one percent, expressed as a decimal fraction (0.0055555...).</summary>
    private static readonly decimal PerMonthRateFirst36 = (5m / 9m) / 100m;

    /// <summary>5/12 of one percent, expressed as a decimal fraction (0.0041666...).</summary>
    private static readonly decimal PerMonthRateBeyond36 = (5m / 12m) / 100m;

    /// <summary>
    /// Returns the fraction (0 to 1) by which the FRA benefit is reduced for claiming
    /// <paramref name="monthsEarly"/> whole months before full retirement age.
    /// The first 36 months are reduced at 5/9 of 1% per month; additional months beyond
    /// 36 are reduced at 5/12 of 1% per month. Claiming exactly at FRA (0 months early)
    /// yields a 0 reduction (100% of the benefit).
    /// </summary>
    public static decimal CalculateReductionFraction(int monthsEarly)
    {
        if (monthsEarly < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthsEarly), "Months early cannot be negative.");
        }

        var monthsInFirstTier = Math.Min(monthsEarly, 36);
        var monthsInSecondTier = Math.Max(monthsEarly - 36, 0);

        return (monthsInFirstTier * PerMonthRateFirst36) + (monthsInSecondTier * PerMonthRateBeyond36);
    }
}
