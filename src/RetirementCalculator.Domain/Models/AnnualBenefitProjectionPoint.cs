using System.Collections;

namespace RetirementCalculator.Domain.Models;

/// <summary>
/// A single annual Social Security benefit point for a claiming scenario.
/// </summary>
public readonly record struct AnnualBenefitProjectionPoint(Age Age, decimal MonthlyBenefit, decimal AnnualBenefit, int YearOffset)
{
    public decimal BenefitForYear => AnnualBenefit;

    public decimal MonthlyAmount => MonthlyBenefit;

    public decimal AnnualAmount => AnnualBenefit;
}

/// <summary>
/// Chronologically ordered annual benefit points for a scenario.
/// </summary>
public sealed class OrderedAnnualBenefitSeries : IReadOnlyList<AnnualBenefitProjectionPoint>
{
    private readonly IReadOnlyList<AnnualBenefitProjectionPoint> _points;

    public OrderedAnnualBenefitSeries(IEnumerable<AnnualBenefitProjectionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.OrderBy(point => point.YearOffset).ToArray();
    }

    public AnnualBenefitProjectionPoint this[int index] => _points[index];

    public int Count => _points.Count;

    public IEnumerator<AnnualBenefitProjectionPoint> GetEnumerator() => _points.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
