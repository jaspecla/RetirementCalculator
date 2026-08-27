using Bunit;
using RetirementCalculator.Domain.Models;
using RetirementCalculator.Web.Components;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class CumulativeIncomeGraphComponentTests
{
    [TestMethod]
    public void Render_WithProjection_RendersAccessibleLegendAndSeriesLabels()
    {
        using var ctx = new BunitContext();
        var projection = new[]
        {
            new CumulativeIncomeProjectionPoint { Age = new Age(62, 0), ChosenAgeCumulativeIncome = 0m, FullRetirementAgeCumulativeIncome = 0m },
            new CumulativeIncomeProjectionPoint { Age = new Age(63, 0), ChosenAgeCumulativeIncome = 12000m, FullRetirementAgeCumulativeIncome = 18000m },
            new CumulativeIncomeProjectionPoint { Age = new Age(64, 0), ChosenAgeCumulativeIncome = 24000m, FullRetirementAgeCumulativeIncome = 36000m },
        };

        var cut = ctx.Render<CumulativeIncomeGraph>(parameters => parameters
            .Add(parameter => parameter.Projection, projection));

        Assert.AreEqual(1, cut.FindAll("svg[role='img']").Count);
        Assert.IsTrue(cut.Markup.Contains("Cumulative income by age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at chosen age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at full retirement age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Cumulative income", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Render_WithIdenticalSeries_UsesValidPathCoordinates()
    {
        using var ctx = new BunitContext();
        var projection = new[]
        {
            new CumulativeIncomeProjectionPoint { Age = new Age(62, 0), ChosenAgeCumulativeIncome = 5000m, FullRetirementAgeCumulativeIncome = 5000m },
            new CumulativeIncomeProjectionPoint { Age = new Age(63, 0), ChosenAgeCumulativeIncome = 10000m, FullRetirementAgeCumulativeIncome = 10000m },
            new CumulativeIncomeProjectionPoint { Age = new Age(64, 0), ChosenAgeCumulativeIncome = 15000m, FullRetirementAgeCumulativeIncome = 15000m },
        };

        var cut = ctx.Render<CumulativeIncomeGraph>(parameters => parameters
            .Add(parameter => parameter.Projection, projection));

        var paths = cut.FindAll("path.series");
        Assert.AreEqual(2, paths.Count);
        foreach (var path in paths)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(path.GetAttribute("d") ?? string.Empty));
            Assert.IsFalse((path.GetAttribute("d") ?? string.Empty).Contains("NaN", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Render_WithAllZeroSeries_UsesBaselineCoordinates()
    {
        using var ctx = new BunitContext();
        var projection = new[]
        {
            new CumulativeIncomeProjectionPoint { Age = new Age(62, 0), ChosenAgeCumulativeIncome = 0m, FullRetirementAgeCumulativeIncome = 0m },
            new CumulativeIncomeProjectionPoint { Age = new Age(63, 0), ChosenAgeCumulativeIncome = 0m, FullRetirementAgeCumulativeIncome = 0m },
        };

        var cut = ctx.Render<CumulativeIncomeGraph>(parameters => parameters
            .Add(parameter => parameter.Projection, projection));

        var pathMarkup = cut.FindAll("path.series");
        Assert.AreEqual(2, pathMarkup.Count);
        foreach (var path in pathMarkup)
        {
            Assert.IsFalse((path.GetAttribute("d") ?? string.Empty).Contains("NaN", StringComparison.Ordinal));
        }
        Assert.IsTrue(cut.Markup.Contains("$0", StringComparison.Ordinal));
    }
}
