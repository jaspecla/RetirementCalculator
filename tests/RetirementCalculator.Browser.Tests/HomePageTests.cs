using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace RetirementCalculator.Browser.Tests;

[TestClass]
public sealed class HomePageTests
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    [TestInitialize]
    public async Task InitializeBrowser()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Channel = "chrome",
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }

    [TestCleanup]
    public async Task CloseBrowser()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [TestMethod]
    public async Task SubmitEmptyForm_ShowsValidationErrors()
    {
        await _page.GotoAsync(BrowserTestHost.BaseUrl);

        await WaitForInteractiveFormAsync();

        await Assertions.Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Results" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText(new Regex(@"Enter a birth year between 1900 and \d{4}\."))).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Enter a monthly benefit at full retirement age greater than $0.")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Enter a claim age with whole years and 0-11 months.")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Enter a planning age with whole years and 0-11 months.")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task SubmitValidForm_ShowsComparisonResults()
    {
        await _page.GotoAsync(BrowserTestHost.BaseUrl);

        await WaitForInteractiveFormAsync();
        var calculateButton = _page.GetByRole(AriaRole.Button, new() { Name = "Calculate" });
        await _page.Locator("#birthYear").FillAsync("1965");
        await _page.Locator("#fraBenefit").FillAsync("2000");
        await _page.Locator("#claimAgeYears").FillAsync("64");
        await _page.Locator("#claimAgeMonths").FillAsync("6");
        await _page.Locator("#planningAgeYears").FillAsync("90");
        await _page.Locator("#planningAgeMonths").FillAsync("0");
        await calculateButton.ClickAsync();
        await ThrowIfBlazorFailedAsync();

        await Assertions.Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Results" })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Table)).ToContainTextAsync("Claim at chosen age");
        await Assertions.Expect(_page.GetByRole(AriaRole.Table)).ToContainTextAsync("Claim at full retirement age");
    }

    private async Task WaitForInteractiveFormAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var alerts = _page.GetByRole(AriaRole.Alert);

        while (DateTime.UtcNow < deadline)
        {
            await _page.GetByRole(AriaRole.Button, new() { Name = "Calculate" }).ClickAsync();
            await ThrowIfBlazorFailedAsync();

            try
            {
                await alerts.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 1_000 });
                if (await alerts.CountAsync() == 4)
                {
                    return;
                }
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.Fail($"The calculator did not become interactive within 10 seconds.{Environment.NewLine}{BrowserTestHost.GetHostOutput()}");
    }

    private async Task ThrowIfBlazorFailedAsync()
    {
        if (await _page.Locator("#blazor-error-ui").IsVisibleAsync())
        {
            Assert.Fail($"Blazor reported an unhandled error.{Environment.NewLine}{BrowserTestHost.GetHostOutput()}");
        }
    }
}