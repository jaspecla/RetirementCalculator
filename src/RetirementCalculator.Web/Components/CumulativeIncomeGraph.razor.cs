using System.Globalization;
using Microsoft.AspNetCore.Components;
using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Web.Components;

public partial class CumulativeIncomeGraph : ComponentBase
{
    internal const double SvgWidth = 720d;
    internal const double SvgHeight = 320d;
    internal const double PlotLeft = 56d;
    internal const double PlotTop = 18d;
    internal const double PlotRight = 20d;
    internal const double PlotBottom = 46d;

    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    [Parameter]
    public IReadOnlyList<AnnualCumulativeIncomePoint>? ChosenAgeSeries { get; set; }

    [Parameter]
    public IReadOnlyList<AnnualCumulativeIncomePoint>? FullRetirementAgeSeries { get; set; }

    [Parameter]
    public string ChosenAgeLabel { get; set; } = "Claim at chosen age";

    [Parameter]
    public string FullRetirementAgeLabel { get; set; } = "Claim at full retirement age";

    [Parameter]
    public string Title { get; set; } = "Annual cumulative income through planning age";

    private IReadOnlyList<AnnualCumulativeIncomePoint> ChosenSeries => ChosenAgeSeries ?? Array.Empty<AnnualCumulativeIncomePoint>();

    private IReadOnlyList<AnnualCumulativeIncomePoint> FraSeries => FullRetirementAgeSeries ?? Array.Empty<AnnualCumulativeIncomePoint>();

    private IReadOnlyList<ChartCoordinate> ChosenCoordinates => BuildCoordinates(ChosenSeries);

    private IReadOnlyList<ChartCoordinate> FraCoordinates => BuildCoordinates(FraSeries);

    private bool HasData => ChosenSeries.Count > 0 || FraSeries.Count > 0;

    private GraphBounds Bounds => GetBounds(ChosenSeries, FraSeries);

    private IReadOnlyList<ChartTick> YTicks => BuildTicks(Bounds);

    private IReadOnlyList<GraphDataRow> TableRows => BuildTableRows();

    private static string FormatCurrency(decimal amount) => amount.ToString("C2", DisplayCulture);

    private static string FormatOptionalCurrency(decimal? amount) => amount is null ? "—" : FormatCurrency(amount.Value);

    private static string FormatAge(Age age) => age.ToString();

    private static string BuildPolylinePoints(IReadOnlyList<ChartCoordinate> coordinates)
        => string.Join(" ", coordinates.Select(point =>
        {
            var x = point.X.ToString("F2", CultureInfo.InvariantCulture);
            var y = point.Y.ToString("F2", CultureInfo.InvariantCulture);
            return $"{x},{y}";
        }));

    private IReadOnlyList<ChartCoordinate> BuildCoordinates(IReadOnlyList<AnnualCumulativeIncomePoint> series)
    {
        if (series.Count == 0)
        {
            return Array.Empty<ChartCoordinate>();
        }

        var bounds = Bounds;
        var plotWidth = SvgWidth - PlotLeft - PlotRight;
        var plotHeight = SvgHeight - PlotTop - PlotBottom;
        var xRange = bounds.MaxX - bounds.MinX;
        var yRange = (double)(bounds.MaxY - bounds.MinY);

        var coordinates = new List<ChartCoordinate>(series.Count);
        foreach (var point in series)
        {
            var x = xRange <= 0
                ? PlotLeft + plotWidth / 2d
                : PlotLeft + ((point.Age.TotalMonths - bounds.MinX) / (double)xRange) * plotWidth;

            var y = yRange <= 0
                ? PlotTop + plotHeight / 2d
                : PlotTop + plotHeight - (((double)point.CumulativeIncome - (double)bounds.MinY) / yRange) * plotHeight;

            coordinates.Add(new ChartCoordinate(x, y));
        }

        return coordinates;
    }

    private GraphBounds GetBounds(IReadOnlyList<AnnualCumulativeIncomePoint> chosen, IReadOnlyList<AnnualCumulativeIncomePoint> fra)
    {
        var allPoints = chosen.Concat(fra).ToList();
        if (allPoints.Count == 0)
        {
            return new GraphBounds(0, 0, 0m, 0m);
        }

        var minX = allPoints.Min(point => point.Age.TotalMonths);
        var maxX = allPoints.Max(point => point.Age.TotalMonths);
        var minY = allPoints.Min(point => point.CumulativeIncome);
        var maxY = allPoints.Max(point => point.CumulativeIncome);

        return new GraphBounds(minX, maxX, minY, maxY);
    }

    private IReadOnlyList<ChartTick> BuildTicks(GraphBounds bounds)
    {
        if (bounds.MaxY == bounds.MinY)
        {
            var y = PlotTop + (SvgHeight - PlotTop - PlotBottom) / 2d;
            return new[] { new ChartTick(bounds.MinY, y) };
        }

        const int tickCount = 4;
        var plotHeight = SvgHeight - PlotTop - PlotBottom;
        var ticks = new List<ChartTick>(tickCount + 1);
        for (var i = 0; i <= tickCount; i++)
        {
            var ratio = i / (double)tickCount;
            var value = bounds.MinY + (bounds.MaxY - bounds.MinY) * (decimal)ratio;
            var y = PlotTop + plotHeight - ratio * plotHeight;
            ticks.Add(new ChartTick(value, y));
        }

        return ticks;
    }

    private IReadOnlyList<GraphDataRow> BuildTableRows()
    {
        var rows = new SortedDictionary<int, GraphDataRow>();

        foreach (var point in ChosenSeries)
        {
            if (!rows.TryGetValue(point.Age.TotalMonths, out var row))
            {
                row = new GraphDataRow(point.Age, null, null);
                rows[point.Age.TotalMonths] = row;
            }

            rows[point.Age.TotalMonths] = row with { ChosenAgeCumulativeIncome = point.CumulativeIncome };
        }

        foreach (var point in FraSeries)
        {
            if (!rows.TryGetValue(point.Age.TotalMonths, out var row))
            {
                row = new GraphDataRow(point.Age, null, null);
                rows[point.Age.TotalMonths] = row;
            }

            rows[point.Age.TotalMonths] = row with { FullRetirementAgeCumulativeIncome = point.CumulativeIncome };
        }

        return rows.Values.ToList();
    }

    private readonly record struct GraphBounds(int MinX, int MaxX, decimal MinY, decimal MaxY);

    private readonly record struct ChartCoordinate(double X, double Y);

    private readonly record struct ChartTick(decimal Value, double Y);

    private readonly record struct GraphDataRow(Age Age, decimal? ChosenAgeCumulativeIncome, decimal? FullRetirementAgeCumulativeIncome);
}
