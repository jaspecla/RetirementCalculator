namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Represents an age expressed as whole years and additional months (0-11).
/// </summary>
public readonly record struct Age(int Years, int Months)
{
    /// <summary>
    /// The age expressed as a total number of whole months.
    /// </summary>
    public int TotalMonths => Years * 12 + Months;

    /// <summary>
    /// Creates an <see cref="Age"/> from a total number of months.
    /// </summary>
    public static Age FromTotalMonths(int totalMonths)
    {
        if (totalMonths < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalMonths), "Total months cannot be negative.");
        }

        return new Age(totalMonths / 12, totalMonths % 12);
    }

    public override string ToString() => Months == 0
        ? $"{Years}"
        : $"{Years} yr {Months} mo";
}
