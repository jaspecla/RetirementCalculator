using System.Globalization;
using Microsoft.AspNetCore.Components;
using RetirementCalculator.Domain.Models;
using RetirementCalculator.Domain.Services;
using RetirementCalculator.Domain.Validation;

namespace RetirementCalculator.Web.Components.Pages;

/// <summary>
/// Code-behind for the Social Security claiming calculator page. Contains only UI glue
/// (binding, validation wiring, formatting); all claiming math lives in
/// <see cref="RetirementCalculator.Domain"/> services.
/// </summary>
public partial class Home : ComponentBase
{
    private readonly SocialSecurityCalculatorInput _input = new();
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();
    private SocialSecurityComparisonResult? _result;

    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    private string? ErrorFor(string field) => _errors.TryGetValue(field, out var message) ? message : null;

    /// <summary>Formats a dollar amount with cents, independent of the host machine's default culture.</summary>
    private static string FormatCurrency(decimal amount) => amount.ToString("C2", DisplayCulture);

    /// <summary>Formats a percentage with one decimal place, independent of the host machine's default culture.</summary>
    private static string FormatPercent(decimal percent) => percent.ToString("N1", DisplayCulture) + "%";

    private void Calculate()
    {
        var validationErrors = SocialSecurityInputValidator.Validate(_input, DateTime.UtcNow.Year);
        _errors = validationErrors.ToDictionary(e => e.Field, e => e.Message);
        _result = _errors.Count == 0 ? SocialSecurityBenefitCalculator.Calculate(_input) : null;
    }
}
