using System.Globalization;
using Bunit;
using RetirementCalculator.Domain.Models;
using RetirementCalculator.Web.Components;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class CumulativeProjectionGraphTests
{
    [TestMethod]
    public void Render_WithDistinctSeries_RendersAccessibleSvgAndScenarioLabels()
    {
        using var ctx = new BunitContext();

        var selected = CreateSeries(62, 1200m, 5);
        var fra = CreateSeries(67, 2000m, 5);

        var cut = ctx.Render<CumulativeProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, selected)
            .Add(p => p.FraSeries, fra)
            .Add(p => p.SelectedSeriesLabel, "Claim at age 62")
            .Add(p => p.FraSeriesLabel, "Claim at full retirement age"));

        var svg = cut.Find("svg[role='img']");
        var description = cut.Find("desc").TextContent;
        var ageTicks = cut.FindAll("text.age-tick-label");
        var currencyTicks = cut.FindAll("text.value-tick-label");

        Assert.IsNotNull(svg);
        Assert.IsNotNull(svg.GetAttribute("aria-labelledby"));
        Assert.AreEqual("Cumulative Social Security income", cut.Find("title").TextContent.Trim());
        Assert.IsTrue(ageTicks.Any(tick => tick.TextContent.Trim() == "62"));
        Assert.IsTrue(ageTicks.Any(tick => tick.TextContent.Trim() == "72"));
        Assert.IsTrue(currencyTicks.Count > 0);
        Assert.IsTrue(currencyTicks.Any(tick => tick.TextContent.Contains('$')));
        Assert.AreEqual("Age", cut.Find("text.x-axis-title").TextContent.Trim());
        Assert.AreEqual("Cumulative income", cut.Find("text.y-axis-title").TextContent.Trim());
        Assert.IsTrue(description.Contains("Ages 62 to 72", StringComparison.Ordinal));
        Assert.IsTrue(description.Contains("Claim at age 62", StringComparison.Ordinal));
        Assert.IsTrue(description.Contains("Claim at full retirement age", StringComparison.Ordinal));
        Assert.IsTrue(description.Contains("$", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at age 62", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at full retirement age", StringComparison.Ordinal));
        Assert.AreEqual(2, cut.FindAll("polyline").Count);
    }

    [TestMethod]
    public void Render_WithPartialYearEndpoint_UsesFinalPlottedAgeInTicksAndDescription()
    {
        using var ctx = new BunitContext();

        var selected = CreateSeriesToAge(62, 1200m, new Age(69, 3));
        var fra = CreateSeriesToAge(67, 2000m, new Age(69, 3));

        var cut = ctx.Render<CumulativeProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, selected)
            .Add(p => p.FraSeries, fra)
            .Add(p => p.SelectedSeriesLabel, "Claim at age 62")
            .Add(p => p.FraSeriesLabel, "Claim at full retirement age"));

        var description = cut.Find("desc").TextContent;
        var ageTickXCoordinates = cut.FindAll("text.age-tick-label")
            .Select(tick => double.Parse(tick.GetAttribute("x") ?? "0", CultureInfo.InvariantCulture))
            .ToList();

        Assert.IsTrue(ageTickXCoordinates.All(x => x >= 70d && x <= 610d));
        Assert.IsTrue(description.Contains("Ages 62 to 69", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("Ages 62 to 70", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("70.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Render_WithCoincidentSeries_UsesDistinctDashPatternsToKeepBothVisible()
    {
        using var ctx = new BunitContext();

        var coincident = CreateSeries(67, 2000m, 4);

        var cut = ctx.Render<CumulativeProjectionGraph>(parameters => parameters
            .Add(p => p.SelectedSeries, coincident)
            .Add(p => p.FraSeries, coincident)
            .Add(p => p.SelectedSeriesLabel, "Claim at FRA")
            .Add(p => p.FraSeriesLabel, "Wait until FRA"));

        var polylines = cut.FindAll("polyline");
        Assert.AreEqual(2, polylines.Count);
        Assert.IsTrue(polylines.Any(line => line.GetAttribute("stroke-dasharray") == "0"));
        Assert.IsTrue(polylines.Any(line => line.GetAttribute("stroke-dasharray") == "8 7"));
        Assert.IsTrue(cut.Markup.Contains("Claim at FRA", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Wait until FRA", StringComparison.Ordinal));
    }

    private static IReadOnlyList<ProjectionPoint> CreateSeriesToAge(int claimAge, decimal benefit, Age endAge)
    {
        var points = new List<ProjectionPoint>();
        var startMonth = claimAge * 12;
        var endMonth = endAge.TotalMonths;

        for (var totalMonth = startMonth; totalMonth <= endMonth; totalMonth += 12)
        {
            var age = Age.FromTotalMonths(totalMonth);
            var cumulative = benefit * Math.Max(totalMonth - startMonth, 0);
            points.Add(new ProjectionPoint(age, cumulative));
        }

        var finalAge = endAge;
        var finalCumulative = benefit * Math.Max(endMonth - startMonth, 0);
        points.Add(new ProjectionPoint(finalAge, finalCumulative));

        return points.DistinctBy(point => point.Age.TotalMonths).ToList();
    }

    private static IReadOnlyList<ProjectionPoint> CreateSeries(int claimAge, decimal benefit, int years)
    {
        var points = new List<ProjectionPoint>();
        for (var year = claimAge; year <= claimAge + years; year++)
        {
            var age = new Age(year, 0);
            var cumulative = benefit * Math.Max(year - claimAge, 0) * 12m;
            points.Add(new ProjectionPoint(age, cumulative));
        }

        return points;
    }
}
