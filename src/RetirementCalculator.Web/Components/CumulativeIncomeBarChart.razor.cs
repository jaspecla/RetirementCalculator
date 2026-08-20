using System.Globalization;
using Microsoft.AspNetCore.Components;
using RetirementCalculator.Domain.Models;

namespace RetirementCalculator.Web.Components;

public partial class CumulativeIncomeBarChart : ComponentBase
{
    private const decimal ChartWidth = 720m;
    private const decimal ChartHeight = 320m;
    private const decimal MarginTop = 24m;
    private const decimal MarginRight = 24m;
    private const decimal MarginBottom = 52m;
    private const decimal MarginLeft = 72m;
    private const decimal GroupGap = 12m;
    private const decimal InnerGap = 6m;

    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    [Parameter, EditorRequired]
    public SocialSecurityComparisonResult Result { get; set; } = default!;

    private IReadOnlyList<ChartYearData> YearData { get; set; } = [];

    private decimal MaxCumulativeAmount { get; set; }

    private decimal PlotWidth => ChartWidth - MarginLeft - MarginRight;

    private decimal PlotHeight => ChartHeight - MarginTop - MarginBottom;

    private bool HasChartData => YearData.Count > 0 && MaxCumulativeAmount > 0m;

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Result);

        var chosenByYear = Result.ChosenAgeScenario.CumulativeIncomeByYear
            .GroupBy(entry => entry.Year)
            .ToDictionary(group => group.Key, group => group.Last().CumulativeAmount);

        var fraByYear = Result.FullRetirementAgeScenario.CumulativeIncomeByYear
            .GroupBy(entry => entry.Year)
            .ToDictionary(group => group.Key, group => group.Last().CumulativeAmount);

        YearData = chosenByYear.Keys
            .Union(fraByYear.Keys)
            .OrderBy(year => year)
            .Select(year => new ChartYearData(
                year,
                chosenByYear.GetValueOrDefault(year),
                fraByYear.GetValueOrDefault(year)))
            .ToArray();

        MaxCumulativeAmount = YearData.Count == 0
            ? 0m
            : YearData.Max(data => Math.Max(data.ChosenAmount, data.FullRetirementAgeAmount));
    }

    private string ChartAriaLabel => HasChartData
        ? $"Bar chart comparing cumulative Social Security income by year for claiming at the chosen age versus full retirement age across {YearData.Count} years."
        : "Cumulative Social Security income chart unavailable because no yearly income data was produced for either scenario.";

    private decimal GroupWidth => YearData.Count == 0
        ? 0m
        : (PlotWidth - (GroupGap * Math.Max(YearData.Count - 1, 0))) / YearData.Count;

    private decimal BarWidth => GroupWidth <= 0m
        ? 0m
        : Math.Max((GroupWidth - InnerGap) / 2m, 1m);

    private decimal XAxisY => MarginTop + PlotHeight;

    private decimal GetGroupX(int index) => MarginLeft + index * (GroupWidth + GroupGap);

    private decimal GetChosenX(int index) => GetGroupX(index);

    private decimal GetFraX(int index) => GetGroupX(index) + BarWidth + InnerGap;

    private decimal GetBarHeight(decimal amount) => MaxCumulativeAmount <= 0m
        ? 0m
        : Math.Round((amount / MaxCumulativeAmount) * PlotHeight, 2);

    private decimal GetBarY(decimal amount) => MarginTop + PlotHeight - GetBarHeight(amount);

    private decimal GetLabelX(int index) => GetGroupX(index) + (GroupWidth / 2m);

    private IEnumerable<decimal> GetYAxisTicks()
    {
        if (MaxCumulativeAmount <= 0m)
        {
            return [];
        }

        return Enumerable.Range(0, 5)
            .Select(step => Math.Round(MaxCumulativeAmount * step / 4m, 2));
    }

    private decimal GetTickY(decimal amount) => MarginTop + PlotHeight - (MaxCumulativeAmount <= 0m
        ? 0m
        : Math.Round((amount / MaxCumulativeAmount) * PlotHeight, 2));

    private static string FormatCurrency(decimal amount) => amount.ToString("C2", DisplayCulture);

    private sealed record ChartYearData(int Year, decimal ChosenAmount, decimal FullRetirementAgeAmount);
}
