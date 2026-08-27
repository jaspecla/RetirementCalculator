using Bunit;
using RetirementCalculator.Domain.Models;
using RetirementCalculator.Web.Components;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class CumulativeIncomeGraphComponentTests
{
    [TestMethod]
    public void Render_WithSeriesData_RendersAccessibleSvgAndDataTable()
    {
        using var ctx = new BunitContext();

        var chosenAgeSeries = new[]
        {
            new AnnualCumulativeIncomePoint(new Age(62, 0), 0m),
            new AnnualCumulativeIncomePoint(new Age(63, 0), 12000m),
            new AnnualCumulativeIncomePoint(new Age(64, 0), 24000m),
            new AnnualCumulativeIncomePoint(new Age(65, 0), 36000m),
        };

        var fraSeries = new[]
        {
            new AnnualCumulativeIncomePoint(new Age(62, 0), 0m),
            new AnnualCumulativeIncomePoint(new Age(65, 0), 36000m),
            new AnnualCumulativeIncomePoint(new Age(66, 0), 48000m),
        };

        var cut = ctx.Render<CumulativeIncomeGraph>(parameters => parameters
            .Add(p => p.ChosenAgeSeries, chosenAgeSeries)
            .Add(p => p.FullRetirementAgeSeries, fraSeries)
            .Add(p => p.ChosenAgeLabel, "Claim at chosen age")
            .Add(p => p.FullRetirementAgeLabel, "Claim at full retirement age"));

        var svg = cut.Find("svg");
        Assert.AreEqual("img", svg.GetAttribute("role"));
        Assert.IsTrue(svg.HasAttribute("aria-labelledby"));
        Assert.IsTrue(cut.Markup.Contains("Claim at chosen age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at full retirement age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Annual cumulative income through planning age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("<polyline", StringComparison.Ordinal));

        Assert.AreEqual(5, cut.FindAll("table tbody tr").Count);
        Assert.AreEqual(3, cut.FindAll("table thead th").Count);
    }

    [TestMethod]
    public void Render_WithFlatOrSinglePointSeries_UsesStableCoordinatesWithoutNaN()
    {
        using var ctx = new BunitContext();

        var chosenAgeSeries = new[]
        {
            new AnnualCumulativeIncomePoint(new Age(65, 0), 5000m),
            new AnnualCumulativeIncomePoint(new Age(65, 0), 5000m),
        };

        var fraSeries = new[]
        {
            new AnnualCumulativeIncomePoint(new Age(65, 0), 5000m)
        };

        var cut = ctx.Render<CumulativeIncomeGraph>(parameters => parameters
            .Add(p => p.ChosenAgeSeries, chosenAgeSeries)
            .Add(p => p.FullRetirementAgeSeries, fraSeries));

        var svgHtml = cut.Find("svg").OuterHtml;
        Assert.IsFalse(svgHtml.Contains("NaN", StringComparison.Ordinal));
        Assert.IsFalse(svgHtml.Contains("Infinity", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Annual cumulative income through planning age", StringComparison.Ordinal));
    }
}
