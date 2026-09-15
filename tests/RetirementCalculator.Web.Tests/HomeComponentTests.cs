using System.Reflection;
using Bunit;
using RetirementCalculator.Domain.Models;
using RetirementCalculator.Web.Components.Pages;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class HomeComponentTests
{
    private static BunitContext CreateContext() => new();

    [TestMethod]
    public void Submit_WithAllFieldsEmpty_ShowsFieldErrorsAndDoesNotRenderResults()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        cut.Find("form").Submit();

        Assert.IsFalse(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));
        var errorMessages = cut.FindAll(".field-error");
        Assert.IsGreaterThan(0, errorMessages.Count);
        Assert.IsTrue(cut.Markup.Contains("Enter a birth year", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter a monthly benefit at full retirement age greater than $0.", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter an initial account balance of $0 or more.", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter a monthly spending amount of $0 or more.", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter a retirement age with whole years and 0-11 months.", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Average inflation must be between 0% and 12%.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Submit_AfterPriorValidResultThenInvalidEdit_ClearsPreviousResultAndShowsError()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        SetValidInputs(cut);
        cut.Find("form").Submit();

        Assert.IsTrue(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Modeled COLA", StringComparison.Ordinal));

        cut.Find("#initialBalance").Input(-1m);
        cut.Find("form").Submit();

        Assert.IsFalse(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter an initial account balance of $0 or more.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Submit_WithAllValidInputs_RendersModeledColaAndBalanceProjection()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        SetValidInputs(cut);
        cut.Find("form").Submit();

        Assert.IsTrue(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Modeled COLA", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("First-year inflated spending", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("COLA-adjusted Social Security income", StringComparison.Ordinal));
        Assert.AreEqual(0, cut.FindAll(".field-error").Count);
        Assert.IsTrue(cut.FindAll("svg[role='img']").Count >= 2);
    }

    [TestMethod]
    public void Submit_ValidInputsTwice_UsesFreshSharedInflationPathEachTime()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        SetValidInputs(cut);
        cut.Find("form").Submit();

        var firstResult = ReadCurrentResult(cut.Instance);
        Assert.IsNotNull(firstResult);
        Assert.AreSame(firstResult.InflationPath, firstResult.ChosenAgeScenario.InflationPath);
        Assert.AreSame(firstResult.InflationPath, firstResult.FullRetirementAgeScenario.InflationPath);

        cut.Find("form").Submit();

        var secondResult = ReadCurrentResult(cut.Instance);
        Assert.IsNotNull(secondResult);
        Assert.AreNotSame(firstResult.InflationPath, secondResult.InflationPath);
        Assert.AreSame(secondResult.InflationPath, secondResult.ChosenAgeScenario.InflationPath);
        Assert.AreSame(secondResult.InflationPath, secondResult.FullRetirementAgeScenario.InflationPath);
    }

    [TestMethod]
    public void Submit_WithFourPercentBoundaryWarning_RendersWarningOnlyWhenAboveThreshold()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        SetValidInputs(cut);
        cut.Find("#initialBalance").Input(60_000m);
        cut.Find("#monthlySpending").Input(3_000m);
        cut.Find("#fraBenefit").Input(1_000m);
        cut.Find("#claimAgeYears").Input(67);
        cut.Find("#claimAgeMonths").Input(0);
        cut.Find("#retirementAgeYears").Input(67);
        cut.Find("#retirementAgeMonths").Input(0);
        cut.Find("form").Submit();

        Assert.IsTrue(cut.Markup.Contains("Warning: first-year net withdrawal", StringComparison.Ordinal));

        cut.Find("#initialBalance").Input(100_000m);
        cut.Find("#monthlySpending").Input(500m);
        cut.Find("#fraBenefit").Input(2_500m);
        cut.Find("form").Submit();

        Assert.IsFalse(cut.Markup.Contains("Warning: first-year net withdrawal", StringComparison.Ordinal));
    }

    private static void SetValidInputs(IRenderedComponent<Home> cut)
    {
        cut.Find("#birthYear").Input(1965);
        cut.Find("#fraBenefit").Input(2_000m);
        cut.Find("#initialBalance").Input(200_000m);
        cut.Find("#monthlySpending").Input(4_000m);
        cut.Find("#averageInflationRate").Input(2.5m);
        cut.Find("#retirementAgeYears").Input(67);
        cut.Find("#retirementAgeMonths").Input(0);
        cut.Find("#claimAgeYears").Input(62);
        cut.Find("#claimAgeMonths").Input(0);
        cut.Find("#planningAgeYears").Input(90);
        cut.Find("#planningAgeMonths").Input(0);
    }

    private static SocialSecurityComparisonResult? ReadCurrentResult(Home component)
    {
        var field = typeof(Home).GetField("_result", BindingFlags.Instance | BindingFlags.NonPublic);
        return (SocialSecurityComparisonResult?)field?.GetValue(component);
    }
}
