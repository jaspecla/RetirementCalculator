using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Domain.Services;

/// <summary>
/// Derives Social Security full retirement age (FRA) from birth year per the SSA schedule.
/// </summary>
public static class FullRetirementAgeCalculator
{
    /// <summary>
    /// Returns the full retirement age for a given birth year using the SSA schedule:
    /// born 1937 or earlier: 65 years 0 months.
    /// born 1938-1942: 65 years plus 2 months for each year after 1937.
    /// born 1943-1954: 66 years 0 months.
    /// born 1955-1959: 66 years plus 2 months for each year after 1954.
    /// born 1960 or later: 67 years 0 months.
    /// </summary>
    public static Age Calculate(int birthYear)
    {
        if (birthYear <= 1937)
        {
            return new Age(65, 0);
        }

        if (birthYear <= 1942)
        {
            var extraMonths = (birthYear - 1937) * 2;
            return Age.FromTotalMonths(65 * 12 + extraMonths);
        }

        if (birthYear <= 1954)
        {
            return new Age(66, 0);
        }

        if (birthYear <= 1959)
        {
            var extraMonths = (birthYear - 1954) * 2;
            return Age.FromTotalMonths(66 * 12 + extraMonths);
        }

        return new Age(67, 0);
    }
}
