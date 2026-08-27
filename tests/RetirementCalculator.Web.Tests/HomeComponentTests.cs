using Bunit;
using RetirementCalculator.Web.Components.Pages;

namespace RetirementCalculator.Web.Tests;

/// <summary>
/// Component-level (bUnit) tests for the Home page's UI/validation glue: submitting the
/// form must surface field errors without rendering a stale/previous result, and a valid
/// submission must render the comparison results section.
/// </summary>
[TestClass]
public sealed class HomeComponentTests
{
    private static Bunit.BunitContext CreateContext() => new();

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
        Assert.IsTrue(cut.Markup.Contains("Enter a monthly benefit", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter a claim age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Enter a planning age", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Submit_AfterPriorValidResultThenInvalidEdit_ClearsPreviousResultAndShowsError()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        SetValidInputs(cut);
        cut.Find("form").Submit();
        Assert.IsTrue(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));

        // Now make the claim age invalid (below the minimum of 62) and resubmit.
        cut.Find("#claimAgeYears").Input(45);
        cut.Find("form").Submit();

        Assert.IsFalse(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Cumulative income by age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim age must be at least 62 years.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Submit_WithAllValidInputs_RendersResultsAndNoFieldErrors()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<Home>();

        SetValidInputs(cut);
        cut.Find("form").Submit();

        Assert.IsTrue(cut.Markup.Contains("<h2>Results</h2>", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Cumulative income by age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at chosen age", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("Claim at full retirement age", StringComparison.Ordinal));
        Assert.AreEqual(0, cut.FindAll(".field-error").Count);
    }

    private static void SetValidInputs(IRenderedComponent<Home> cut)
    {
        cut.Find("#birthYear").Input(1965);
        cut.Find("#fraBenefit").Input(2000m);
        cut.Find("#claimAgeYears").Input(64);
        cut.Find("#claimAgeMonths").Input(6);
        cut.Find("#planningAgeYears").Input(90);
        cut.Find("#planningAgeMonths").Input(0);
    }
}
