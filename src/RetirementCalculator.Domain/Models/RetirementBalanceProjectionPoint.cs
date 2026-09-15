using System.Collections;

namespace RetirementCalculator.Domain.Models;

/// <summary>
/// A projected annual account-balance point for a single claiming scenario.
/// </summary>
public readonly record struct RetirementBalanceProjectionPoint(
    int YearOffset,
    Age Age,
    decimal BeginningBalance,
    decimal AnnualExpense,
    decimal AnnualSocialSecurityIncome,
    decimal NetWithdrawal,
    decimal EndingBalance)
{
    public decimal AnnualNetWithdrawal => NetWithdrawal;

    public decimal Expense => AnnualExpense;

    public decimal SocialSecurityIncome => AnnualSocialSecurityIncome;
}

/// <summary>
/// Chronologically ordered retirement-balance data for a scenario.
/// </summary>
public sealed class OrderedRetirementBalanceProjectionSeries : IReadOnlyList<RetirementBalanceProjectionPoint>
{
    private readonly IReadOnlyList<RetirementBalanceProjectionPoint> _points;

    public OrderedRetirementBalanceProjectionSeries(IEnumerable<RetirementBalanceProjectionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.OrderBy(point => point.YearOffset).ToArray();
    }

    public RetirementBalanceProjectionPoint this[int index] => _points[index];

    public int Count => _points.Count;

    public IEnumerator<RetirementBalanceProjectionPoint> GetEnumerator() => _points.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Annual retirement-account projection results for a single claiming scenario.
/// </summary>
public sealed class RetirementBalanceProjectionResult
{
    public required Age RetirementAge { get; init; }

    public required Age PlanningAge { get; init; }

    public required decimal InitialAccountBalance { get; init; }

    public decimal InitialBalance => InitialAccountBalance;

    public required OrderedRetirementBalanceProjectionSeries ProjectionSeries { get; init; }

    public decimal FirstYearNetWithdrawal { get; set; }

    public decimal GuidelineThreshold { get; set; }

    public bool IsFirstYearNetWithdrawalAboveFourPercentGuideline { get; set; }

    public Age? FirstDepletionAge { get; set; }

    public int? FirstDepletionCalendarYear { get; set; }

    public Age? DepletionAge => FirstDepletionAge;
}
