using System.Collections;

namespace RetirementCalculator.Domain.Models;

/// <summary>
/// A single annual inflation rate point, expressed as a percentage such as 2.5 for 2.5%.
/// </summary>
public readonly record struct InflationRatePoint(int YearOffset, decimal Rate)
{
    public int Year => YearOffset;

    public decimal InflationRate => Rate;
}

/// <summary>
/// An ordered list of inflation points keyed by year offset from the start of projection.
/// </summary>
public sealed class OrderedInflationRateSeries : IReadOnlyList<InflationRatePoint>
{
    private readonly IReadOnlyList<InflationRatePoint> _points;

    public OrderedInflationRateSeries(IEnumerable<InflationRatePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.OrderBy(point => point.YearOffset).ToArray();
    }

    public InflationRatePoint this[int index] => _points[index];

    public int Count => _points.Count;

    public IEnumerator<InflationRatePoint> GetEnumerator() => _points.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public decimal RateAt(int yearOffset)
    {
        var point = _points.FirstOrDefault(p => p.YearOffset == yearOffset);
        return point.Rate;
    }
}

/// <summary>
/// The generated inflation path for one retirement and benefit projection run.
/// </summary>
public sealed class InflationPathResult
{
    public required decimal AverageInflationRate { get; init; }

    public decimal AverageInflationRatePercent => AverageInflationRate;

    public required OrderedInflationRateSeries Rates { get; init; }

    public IReadOnlyList<decimal> AnnualRates => Rates.Select(point => point.Rate).ToArray();

    public IReadOnlyList<decimal> InflationRates => AnnualRates;

    public int Count => Rates.Count;

    public decimal this[int index] => Rates[index].Rate;
}
