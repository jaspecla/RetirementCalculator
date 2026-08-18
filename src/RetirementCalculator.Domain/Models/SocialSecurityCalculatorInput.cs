namespace RetirementCalculator.Domain.Models;

/// <summary>
/// Raw user-supplied inputs for a Social Security claiming comparison.
/// All fields are required; validate with <see cref="Validation.SocialSecurityInputValidator"/>
/// before passing to <see cref="Services.SocialSecurityBenefitCalculator"/>.
/// </summary>
public sealed class SocialSecurityCalculatorInput
{
    /// <summary>
    /// Four-digit calendar year of birth. Determines the full retirement age (FRA).
    /// </summary>
    public int? BirthYear { get; set; }

    /// <summary>
    /// The estimated monthly benefit payable if the worker claims exactly at full
    /// retirement age (the "PIA", primary insurance amount). Must be greater than zero.
    /// </summary>
    public decimal? MonthlyBenefitAtFullRetirementAge { get; set; }

    /// <summary>
    /// Whole years portion of the age at which the worker is considering claiming.
    /// </summary>
    public int? ClaimAgeYears { get; set; }

    /// <summary>
    /// Additional months (0-11) portion of the age at which the worker is considering claiming.
    /// </summary>
    public int? ClaimAgeMonths { get; set; }

    /// <summary>
    /// Whole years portion of the age through which benefits should be projected.
    /// </summary>
    public int? PlanningAgeYears { get; set; }

    /// <summary>
    /// Additional months (0-11) portion of the age through which benefits should be projected.
    /// </summary>
    public int? PlanningAgeMonths { get; set; }
}
