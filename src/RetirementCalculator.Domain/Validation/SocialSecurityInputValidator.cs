using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Domain.Validation;

/// <summary>
/// Validates raw <see cref="SocialSecurityCalculatorInput"/> before it is passed to the
/// benefit calculator. Produces field-scoped errors suitable for inline UI validation.
/// </summary>
public static class SocialSecurityInputValidator
{
    private const int MinimumClaimAgeYears = 62;
    private const int MinimumBirthYear = 1900;

    /// <summary>
    /// Validates the supplied input. Returns an empty list when the input is valid.
    /// </summary>
    public static IReadOnlyList<ValidationError> Validate(SocialSecurityCalculatorInput input, int currentYear)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<ValidationError>();

        var birthYearValid = input.BirthYear is int birthYear && birthYear >= MinimumBirthYear && birthYear <= currentYear;
        if (!birthYearValid)
        {
            errors.Add(new ValidationError(nameof(input.BirthYear),
                $"Enter a birth year between {MinimumBirthYear} and {currentYear}."));
        }

        if (input.MonthlyBenefitAtFullRetirementAge is not decimal monthlyBenefit || monthlyBenefit <= 0m)
        {
            errors.Add(new ValidationError(nameof(input.MonthlyBenefitAtFullRetirementAge),
                "Enter a monthly benefit at full retirement age greater than $0."));
        }

        var claimAgeMonthsValid = IsValidMonthsComponent(input.ClaimAgeMonths);
        var claimAgeStructurallyValid = input.ClaimAgeYears is int claimAgeYears && claimAgeYears >= 0 && claimAgeMonthsValid;
        if (!claimAgeStructurallyValid)
        {
            errors.Add(new ValidationError(nameof(input.ClaimAgeYears),
                "Enter a claim age with whole years and 0-11 months."));
        }

        var planningAgeMonthsValid = IsValidMonthsComponent(input.PlanningAgeMonths);
        var planningAgeStructurallyValid = input.PlanningAgeYears is int planningAgeYears && planningAgeYears >= 0 && planningAgeMonthsValid;
        if (!planningAgeStructurallyValid)
        {
            errors.Add(new ValidationError(nameof(input.PlanningAgeYears),
                "Enter a planning age with whole years and 0-11 months."));
        }

        // Each independent, structurally-valid field is range-checked on its own so that an
        // error on one field never suppresses an otherwise-detectable error on another.
        Age? claimAge = claimAgeStructurallyValid ? new Age(input.ClaimAgeYears!.Value, input.ClaimAgeMonths!.Value) : null;
        Age? planningAge = planningAgeStructurallyValid ? new Age(input.PlanningAgeYears!.Value, input.PlanningAgeMonths!.Value) : null;
        Age? fullRetirementAge = birthYearValid ? Services.FullRetirementAgeCalculator.Calculate(input.BirthYear!.Value) : null;

        // Claim age must be at least 62 whenever the claim age components are structurally valid,
        // regardless of whether birth year, planning age, or benefit are also valid.
        if (claimAge is Age validClaimAge)
        {
            var minimumClaimAge = new Age(MinimumClaimAgeYears, 0);
            if (validClaimAge.TotalMonths < minimumClaimAge.TotalMonths)
            {
                errors.Add(new ValidationError(nameof(input.ClaimAgeYears),
                    $"Claim age must be at least {MinimumClaimAgeYears} years."));
            }
            // The FRA upper-bound check requires a derivable FRA (a valid birth year) in addition
            // to a structurally valid claim age. It is skipped (not duplicated) when the minimum
            // age check above already reported an error for this field.
            else if (fullRetirementAge is Age fraForClaimCheck && validClaimAge.TotalMonths > fraForClaimCheck.TotalMonths)
            {
                errors.Add(new ValidationError(nameof(input.ClaimAgeYears),
                    $"Claim age cannot be after full retirement age ({fraForClaimCheck})."));
            }
        }

        // The planning-later check is evaluated whenever the claim age and planning age are both
        // structurally valid, using FRA when it is also derivable (a valid birth year). This is
        // independent of any range errors already recorded against the claim age field.
        if (claimAge is Age claimAgeForPlanning && planningAge is Age validPlanningAge)
        {
            var latestRequiredMonths = fullRetirementAge is Age fraForPlanningCheck
                ? Math.Max(claimAgeForPlanning.TotalMonths, fraForPlanningCheck.TotalMonths)
                : claimAgeForPlanning.TotalMonths;

            if (validPlanningAge.TotalMonths <= latestRequiredMonths)
            {
                errors.Add(new ValidationError(nameof(input.PlanningAgeYears),
                    "Planning age must be later than both the claim age and full retirement age."));
            }
        }

        return errors;
    }

    private static bool IsValidMonthsComponent(int? months) => months is int m && m is >= 0 and <= 11;
}
