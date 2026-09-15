using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Domain.Services;

/// <summary>
/// Generates a single annual inflation path for a retirement projection.
/// </summary>
public static class InflationPathCalculator
{
    private const decimal MinimumAnnualRatePercent = 0m;
    private const decimal MaximumAnnualRatePercent = 12m;
    private const decimal RandomChangeMinimumPercent = -1.5m;
    private const decimal RandomChangeMaximumPercent = 1.5m;

    public static InflationPathResult Calculate(decimal averageInflationRatePercent, int yearCount, Func<decimal>? randomChangeProvider = null)
    {
        if (yearCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(yearCount), "Year count cannot be negative.");
        }

        if (yearCount == 0)
        {
            return new InflationPathResult
            {
                AverageInflationRate = averageInflationRatePercent,
                Rates = new OrderedInflationRateSeries(Array.Empty<InflationRatePoint>()),
            };
        }

        var points = new List<InflationRatePoint>(yearCount);
        var safeRandomChangeProvider = randomChangeProvider ?? (() => NextDecimal(Random.Shared, RandomChangeMinimumPercent, RandomChangeMaximumPercent));
        var previousDeviation = 0m;

        for (var yearOffset = 0; yearOffset < yearCount; yearOffset++)
        {
            decimal annualRate;
            if (yearOffset == 0)
            {
                annualRate = averageInflationRatePercent;
            }
            else
            {
                var randomChange = Clamp(safeRandomChangeProvider(), RandomChangeMinimumPercent, RandomChangeMaximumPercent);
                var deviationFromAverage = previousDeviation * 0.7m + randomChange;
                annualRate = averageInflationRatePercent + deviationFromAverage;
                annualRate = Clamp(annualRate, MinimumAnnualRatePercent, MaximumAnnualRatePercent);
                previousDeviation = annualRate - averageInflationRatePercent;
            }

            points.Add(new InflationRatePoint(yearOffset, annualRate));
        }

        return new InflationPathResult
        {
            AverageInflationRate = averageInflationRatePercent,
            Rates = new OrderedInflationRateSeries(points),
        };
    }

    public static InflationPathResult Calculate(decimal averageInflationRatePercent, int yearCount, Random randomSource)
        => Calculate(averageInflationRatePercent, yearCount, () => NextDecimal(randomSource, RandomChangeMinimumPercent, RandomChangeMaximumPercent));

    private static decimal NextDecimal(Random randomSource, decimal minimumInclusive, decimal maximumInclusive)
    {
        ArgumentNullException.ThrowIfNull(randomSource);

        var ratio = randomSource.NextDouble();
        return minimumInclusive + (decimal)ratio * (maximumInclusive - minimumInclusive);
    }

    private static decimal Clamp(decimal value, decimal minimumInclusive, decimal maximumInclusive)
    {
        if (value < minimumInclusive)
        {
            return minimumInclusive;
        }

        if (value > maximumInclusive)
        {
            return maximumInclusive;
        }

        return value;
    }
}
