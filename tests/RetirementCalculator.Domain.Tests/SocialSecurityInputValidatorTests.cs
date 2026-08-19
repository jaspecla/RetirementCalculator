using RetirementCalculator.Domain.Models;
using RetirementCalculator.Domain.Validation;

namespace RetirementCalculator.Domain.Tests;

[TestClass]
public sealed class SocialSecurityInputValidatorTests
{
    private const int CurrentYear = 2026;

    [TestMethod]
    public void Validate_AllFieldsMissing_ReturnsErrorsForEachRequiredField()
    {
        var input = new SocialSecurityCalculatorInput();

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.BirthYear)));
        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.MonthlyBenefitAtFullRetirementAge)));
        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears)));
        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.PlanningAgeYears)));
    }

    [TestMethod]
    public void Validate_MonthlyBenefitZeroOrNegative_ReturnsError()
    {
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 0m,
            ClaimAgeYears = 62,
            ClaimAgeMonths = 0,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.MonthlyBenefitAtFullRetirementAge)));
    }

    [TestMethod]
    public void Validate_ClaimAgeBelowSixtyTwo_ReturnsError()
    {
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 61,
            ClaimAgeMonths = 11,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears)));
    }

    [TestMethod]
    public void Validate_ClaimAgeAfterFullRetirementAge_ReturnsError()
    {
        // FRA for 1965 is 67 years 0 months; 67y1m is after FRA and out of scope.
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 67,
            ClaimAgeMonths = 1,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears)));
    }

    [TestMethod]
    public void Validate_PlanningAgeNotLaterThanBothClaimAgeAndFra_ReturnsError()
    {
        // FRA for 1965 is 67; planning age equal to FRA is not later than both ages.
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 62,
            ClaimAgeMonths = 0,
            PlanningAgeYears = 67,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.PlanningAgeYears)));
    }

    [TestMethod]
    public void Validate_AllFieldsValid_ReturnsNoErrors()
    {
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 64,
            ClaimAgeMonths = 6,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Validate_ClaimAgeBelowSixtyTwoWithInvalidBirthYear_ReturnsErrorsForBothFieldsIndependently()
    {
        // An invalid birth year must not suppress an independently detectable claim-age error.
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1800,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 61,
            ClaimAgeMonths = 11,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.BirthYear)));
        Assert.IsTrue(errors.Any(e =>
            e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears) &&
            e.Message.Contains("must be at least", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_MonthlyBenefitInvalidWithClaimAgeAfterFra_ReturnsErrorsForBothFieldsIndependently()
    {
        // An invalid monthly benefit must not suppress the independently detectable FRA
        // upper-bound error, since the birth year needed to derive FRA is itself valid.
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 0m,
            ClaimAgeYears = 67,
            ClaimAgeMonths = 1,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.MonthlyBenefitAtFullRetirementAge)));
        Assert.IsTrue(errors.Any(e =>
            e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears) &&
            e.Message.Contains("full retirement age", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_InvalidBirthYearWithStructurallyValidClaimAndPlanningAges_ReturnsPlanningErrorRelativeToClaimAgeOnly()
    {
        // FRA cannot be derived because the birth year is invalid, but the planning-later
        // check should still fire relative to the claim age alone.
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1800,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 65,
            ClaimAgeMonths = 0,
            PlanningAgeYears = 65,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.BirthYear)));
        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.PlanningAgeYears)));
    }

    [TestMethod]
    public void Validate_ClaimAgeAndPlanningAgeBothInvalidWithMissingBirthYear_ReturnsAllThreeIndependentErrors()
    {
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = null,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 61,
            ClaimAgeMonths = 11,
            PlanningAgeYears = 61,
            PlanningAgeMonths = 11,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.BirthYear)));
        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears)));
        Assert.IsTrue(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.PlanningAgeYears)));
    }

    [TestMethod]
    public void Validate_ClaimAgeExactlySixtyTwoYearsZeroMonths_IsInclusiveAndDoesNotReturnMinimumAgeError()
    {
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 62,
            ClaimAgeMonths = 0,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsFalse(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears)));
    }

    [TestMethod]
    public void Validate_ClaimAgeExactlyAtFullRetirementAge_IsInclusiveAndDoesNotReturnFraError()
    {
        // FRA for 1965 is 67 years 0 months; claiming exactly at FRA is allowed.
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = 1965,
            MonthlyBenefitAtFullRetirementAge = 2000m,
            ClaimAgeYears = 67,
            ClaimAgeMonths = 0,
            PlanningAgeYears = 90,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        Assert.IsFalse(errors.Any(e => e.Field == nameof(SocialSecurityCalculatorInput.ClaimAgeYears)));
    }

    [TestMethod]
    public void Validate_EachFieldReturnsAtMostOneError_EvenWhenMultipleRulesCouldApply()
    {
        var input = new SocialSecurityCalculatorInput
        {
            BirthYear = null,
            MonthlyBenefitAtFullRetirementAge = null,
            ClaimAgeYears = 40,
            ClaimAgeMonths = 0,
            PlanningAgeYears = 30,
            PlanningAgeMonths = 0,
        };

        var errors = SocialSecurityInputValidator.Validate(input, CurrentYear);

        var groupedByField = errors.GroupBy(e => e.Field);
        Assert.IsTrue(groupedByField.All(g => g.Count() == 1));
    }
}
