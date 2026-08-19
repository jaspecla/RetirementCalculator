namespace RetirementCalculator.Domain.Validation;

/// <summary>
/// A single field-level validation failure.
/// </summary>
public sealed record ValidationError(string Field, string Message);
