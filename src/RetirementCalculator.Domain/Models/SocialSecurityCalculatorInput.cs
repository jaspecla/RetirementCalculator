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
    /// Current calendar year used as the projection start when no explicit override is provided.
    /// Defaults to the runtime year to keep the projection aligned to real calendar years.
    /// </summary>
    public int? CurrentYear { get; set; }

    /// <summary>
    /// The estimated monthly benefit payable if the worker claims exactly at full
    /// retirement age (the "PIA", primary insurance amount). Must be greater than zero.
    /// </summary>
    public decimal? MonthlyBenefitAtFullRetirementAge { get; set; }

    /// <summary>
    /// The amount available in retirement accounts at the start of retirement.
    /// </summary>
    public decimal? InitialAccountBalance { get; set; }

    /// <summary>
    /// Monthly spending in today's dollars during retirement.
    /// </summary>
    public decimal? MonthlySpendingInTodaysDollars { get; set; }

    /// <summary>
    /// Whole years portion of the planned retirement age.
    /// </summary>
    public int? RetirementAgeYears { get; set; }

    /// <summary>
    /// Additional months (0-11) portion of the planned retirement age.
    /// </summary>
    public int? RetirementAgeMonths { get; set; }

    /// <summary>
    /// Average annual inflation assumed for the spending projection, stored as a percent.
    /// Default is 2.5% to align with a typical conservative planning assumption.
    /// </summary>
    public decimal? AverageInflationRate { get; set; } = 2.5m;

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
