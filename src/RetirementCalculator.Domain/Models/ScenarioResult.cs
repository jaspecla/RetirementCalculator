using System.Collections;

namespace RetirementCalculator.Domain.Models;

/// <summary>
/// A single cumulative total point for a Social Security projection.
/// </summary>
public readonly record struct ProjectionPoint(Age Age, decimal CumulativeTotal);

/// <summary>
/// Chronologically ordered projection points representing cumulative nominal dollars received
/// through a series of ages, including annual points and a final partial-year point when needed.
/// </summary>
public sealed class OrderedCumulativeSeries : IReadOnlyList<ProjectionPoint>
{
    private readonly IReadOnlyList<ProjectionPoint> _points;

    public OrderedCumulativeSeries(IEnumerable<ProjectionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        _points = points
            .OrderBy(point => point.Age.TotalMonths)
            .ToArray();

        if (_points.Count == 0)
        {
            throw new ArgumentException("Projection series cannot be empty.", nameof(points));
        }
    }

    public ProjectionPoint this[int index] => _points[index];

    public int Count => _points.Count;

    public ProjectionPoint FinalPoint => _points[^1];

    public decimal FinalCumulativeTotal => FinalPoint.CumulativeTotal;

    public IEnumerator<ProjectionPoint> GetEnumerator() => _points.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Projected results for a single claiming age, used to build a side-by-side comparison.
/// </summary>
public sealed class ScenarioResult
{
    public required Age ClaimAge { get; init; }

    /// <summary>Constant nominal monthly benefit amount for this scenario.</summary>
    public required decimal MonthlyBenefit { get; init; }

    /// <summary>Constant nominal annual benefit amount for this scenario (monthly * 12).</summary>
    public decimal AnnualBenefit => MonthlyBenefit * 12m;

    /// <summary>Number of monthly payments received between claiming and the planning age (inclusive of the claim month, exclusive after the planning age is reached).</summary>
    public required int PaymentMonthsThroughPlanningAge { get; init; }

    /// <summary>Total nominal dollars received from claiming through the planning age.</summary>
    public decimal CumulativeTotalThroughPlanningAge => MonthlyBenefit * PaymentMonthsThroughPlanningAge;

    /// <summary>Chronologically ordered cumulative values for this scenario.</summary>
    public required OrderedCumulativeSeries ProjectionSeries { get; init; }

    /// <summary>Alias for <see cref="ProjectionSeries"/> to keep graphing code explicit.</summary>
    public IReadOnlyList<ProjectionPoint> ProjectionPoints => ProjectionSeries;

    /// <summary>Alias for <see cref="ProjectionSeries"/>.</summary>
    public OrderedCumulativeSeries CumulativeProjection => ProjectionSeries;

    /// <summary>Alias for <see cref="ProjectionSeries"/>.</summary>
    public IReadOnlyList<ProjectionPoint> CumulativeProjectionPoints => ProjectionSeries;

    /// <summary>Alias for <see cref="ProjectionSeries"/>.</summary>
    public IReadOnlyList<ProjectionPoint> CumulativeSeries => ProjectionSeries;

    /// <summary>The final cumulative projection point for the scenario.</summary>
    public ProjectionPoint FinalProjectionPoint => ProjectionSeries.FinalPoint;
}
