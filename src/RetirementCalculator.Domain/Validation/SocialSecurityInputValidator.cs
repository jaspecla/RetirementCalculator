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

        if (input.BirthYear is not int birthYear || birthYear < MinimumBirthYear || birthYear > currentYear)
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
        if (input.ClaimAgeYears is not int claimAgeYears || claimAgeYears < 0 || !claimAgeMonthsValid)
        {
            errors.Add(new ValidationError(nameof(input.ClaimAgeYears),
                "Enter a claim age with whole years and 0-11 months."));
        }

        var planningAgeMonthsValid = IsValidMonthsComponent(input.PlanningAgeMonths);
        if (input.PlanningAgeYears is not int planningAgeYears || planningAgeYears < 0 || !planningAgeMonthsValid)
        {
            errors.Add(new ValidationError(nameof(input.PlanningAgeYears),
                "Enter a planning age with whole years and 0-11 months."));
        }

        // Range checks that depend on a valid birth year require FRA to be derivable first.
        if (errors.Count == 0 &&
            input.BirthYear is int validBirthYear &&
            input.ClaimAgeYears is int validClaimYears &&
            input.ClaimAgeMonths is int validClaimMonths &&
            input.PlanningAgeYears is int validPlanningYears &&
            input.PlanningAgeMonths is int validPlanningMonths)
        {
            var fullRetirementAge = Services.FullRetirementAgeCalculator.Calculate(validBirthYear);
            var claimAge = new Age(validClaimYears, validClaimMonths);
            var planningAge = new Age(validPlanningYears, validPlanningMonths);
            var minimumClaimAge = new Age(MinimumClaimAgeYears, 0);

            if (claimAge.TotalMonths < minimumClaimAge.TotalMonths)
            {
                errors.Add(new ValidationError(nameof(input.ClaimAgeYears),
                    $"Claim age must be at least {MinimumClaimAgeYears} years."));
            }
            else if (claimAge.TotalMonths > fullRetirementAge.TotalMonths)
            {
                errors.Add(new ValidationError(nameof(input.ClaimAgeYears),
                    $"Claim age cannot be after full retirement age ({fullRetirementAge})."));
            }

            var latestRequiredMonths = Math.Max(claimAge.TotalMonths, fullRetirementAge.TotalMonths);
            if (planningAge.TotalMonths <= latestRequiredMonths)
            {
                errors.Add(new ValidationError(nameof(input.PlanningAgeYears),
                    "Planning age must be later than both the claim age and full retirement age."));
            }
        }

        return errors;
    }

    private static bool IsValidMonthsComponent(int? months) => months is int m && m is >= 0 and <= 11;
}
