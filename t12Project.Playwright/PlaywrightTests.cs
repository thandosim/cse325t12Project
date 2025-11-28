using System.Threading.Tasks;
using Microsoft.Playwright;

namespace t12Project.Playwright;

[Parallelizable(ParallelScope.Self)]
public class PlaywrightTests
{
    private static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASEURL")
        ?? "https://localhost:7218";

    [Test]
    public async Task Home_ShouldLoadAndShowTitle()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(BaseUrl);
        var h1 = page.Locator("h1").First;
        await Expect(h1).ToContainTextAsync("Connect Drivers");
    }

    private static ILocatorAssertions Expect(ILocator locator) => Microsoft.Playwright.Assertions.Expect(locator);
}
