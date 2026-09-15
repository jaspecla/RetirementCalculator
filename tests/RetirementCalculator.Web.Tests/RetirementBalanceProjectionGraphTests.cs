using System.Globalization;
using Bunit;
using RetirementCalculator.Domain.Models;
using RetirementCalculator.Web.Components;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class RetirementBalanceProjectionGraphTests
{
    [TestMethod]
    public void Render_WithDistinctSeries_RendersAccessibleSvgAndScenarioLabels()
    {
        using var ctx = new BunitContext();

        var selected = CreateSeries(
            (62, 120_000m),
            (64, 90_000m),
            (66, 60_000m),
            (68, 30_000m),
            (70, 20_000m));
        var fra = CreateSeries(
            (62, 150_000m),
            (64, 120_000m),
            (66, 90_000m),
            (68, 60_000m),
            (70, 30_000m),
            (72, 20_000m));

        var cut = ctx.Render<RetirementBalanceProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, selected)
            .Add(p => p.FraSeries, fra)
            .Add(p => p.SelectedSeriesLabel, "Chosen claim age")
            .Add(p => p.FraSeriesLabel, "Full retirement age"));

        var svg = cut.Find("svg[role='img']");
        var description = cut.Find("desc").TextContent;
        var ageTicks = cut.FindAll("text.age-tick-label");
        var valueTicks = cut.FindAll("text.value-tick-label");

        Assert.IsNotNull(svg);
        Assert.IsNotNull(svg.GetAttribute("aria-labelledby"));
        Assert.AreEqual("Retirement account balance", cut.Find("title").TextContent.Trim());
        Assert.IsTrue(ageTicks.Any(tick => tick.TextContent.Trim() == "62"));
        Assert.IsTrue(ageTicks.Any(tick => tick.TextContent.Trim() == "72"));
        Assert.IsTrue(valueTicks.Count > 0);
        Assert.IsTrue(valueTicks.Any(tick => tick.TextContent.Contains('$')));
        Assert.AreEqual("Age", cut.Find("text.x-axis-title").TextContent.Trim());
        Assert.AreEqual("Account balance", cut.Find("text.y-axis-title").TextContent.Trim());
        Assert.IsTrue(description.Contains("Social Security offsets include modeled COLA", StringComparison.Ordinal));
        Assert.IsTrue(description.Contains("Chosen claim age", StringComparison.Ordinal));
        Assert.IsTrue(description.Contains("Full retirement age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Chosen claim age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Full retirement age", StringComparison.Ordinal));
        Assert.AreEqual(2, cut.FindAll("polyline").Count);
        Assert.AreEqual(0, cut.FindAll("g.depletion-marker").Count);
    }

    [TestMethod]
    public void Render_WithCoincidentSeries_UsesDistinctDashPatternsToKeepBothVisible()
    {
        using var ctx = new BunitContext();

        var coincident = CreateSeries(
            (62, 125_000m),
            (64, 100_000m),
            (66, 75_000m),
            (68, 50_000m),
            (70, 25_000m),
            (72, 15_000m));

        var cut = ctx.Render<RetirementBalanceProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, coincident)
            .Add(p => p.FraSeries, coincident)
            .Add(p => p.SelectedSeriesLabel, "Chosen claim age")
            .Add(p => p.FraSeriesLabel, "Wait until FRA"));

        var polylines = cut.FindAll("polyline");
        Assert.AreEqual(2, polylines.Count);
        Assert.IsTrue(polylines.Any(line => line.GetAttribute("stroke-dasharray") == "0"));
        Assert.IsTrue(polylines.Any(line => line.GetAttribute("stroke-dasharray") == "8 7"));
        Assert.IsTrue(cut.Markup.Contains("Chosen claim age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Wait until FRA", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Render_WithZeroScale_UsesZeroBaselineAndZeroBalanceMarkers()
    {
        using var ctx = new BunitContext();

        var selected = CreateSeries(
            (62, 0m),
            (63, 0m),
            (64, 0m));
        var fra = CreateSeries(
            (62, 0m),
            (63, 0m),
            (64, 0m));

        var cut = ctx.Render<RetirementBalanceProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, selected)
            .Add(p => p.FraSeries, fra)
            .Add(p => p.SelectedSeriesLabel, "Chosen claim age")
            .Add(p => p.FraSeriesLabel, "Full retirement age"));

        var valueTicks = cut.FindAll("text.value-tick-label");
        var markers = cut.FindAll("g.depletion-marker");

        Assert.IsTrue(valueTicks.Any(tick => tick.TextContent.Contains("$0")));
        Assert.AreEqual(2, markers.Count);
        Assert.IsTrue(markers.Any(marker => marker.TextContent.Contains("Age 62", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Render_WithOneDepletedSeries_ShowsMarkerOnlyForTheDepletedScenario()
    {
        using var ctx = new BunitContext();

        var selected = CreateSeries(
            (62, 120_000m),
            (64, 80_000m),
            (66, 30_000m),
            (68, 10_000m),
            (70, 10_000m));
        var fra = CreateSeries(
            (62, 100_000m),
            (64, 60_000m),
            (66, 0m),
            (68, 0m));

        var cut = ctx.Render<RetirementBalanceProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, selected)
            .Add(p => p.FraSeries, fra)
            .Add(p => p.SelectedSeriesLabel, "Chosen claim age")
            .Add(p => p.FraSeriesLabel, "Full retirement age"));

        var markers = cut.FindAll("g.depletion-marker");
        Assert.AreEqual(1, markers.Count);
        Assert.IsTrue(markers[0].TextContent.Contains("Age 66", StringComparison.Ordinal));
        Assert.IsTrue(cut.Find("desc").TextContent.Contains("Full retirement age reaches zero balance at age 66", StringComparison.Ordinal));
    }

    private static IReadOnlyList<RetirementBalanceProjectionPoint> CreateSeries(params (int Age, decimal EndingBalance)[] points)
    {
        var series = new List<RetirementBalanceProjectionPoint>();
        for (var index = 0; index < points.Length; index++)
        {
            var (age, endingBalance) = points[index];
            var pointAge = new Age(age, 0);
            series.Add(new RetirementBalanceProjectionPoint(
                index,
                pointAge,
                endingBalance,
                0m,
                0m,
                0m,
                endingBalance));
        }

        return series;
    }
}
