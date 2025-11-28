using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace t12Project.Playwright;

[Parallelizable(ParallelScope.Self)]
public class PlaywrightTests
{
    private static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASEURL")
        ?? "https://localhost:7218";

    private static string AdminEmail =>
        Environment.GetEnvironmentVariable("E2E_ADMIN_EMAIL") ?? "admin@loadhitch.com";

    private static string AdminPassword =>
        Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD") ?? "Admin@123456!";

    private static string DriverEmail =>
        Environment.GetEnvironmentVariable("E2E_DRIVER_EMAIL") ?? "driver@loadhitch.com";

    private static string DriverPassword =>
        Environment.GetEnvironmentVariable("E2E_DRIVER_PASSWORD") ?? "Driver@123456!";

    private static string CustomerEmail =>
        Environment.GetEnvironmentVariable("E2E_CUSTOMER_EMAIL") ?? "customer@loadhitch.com";

    private static string CustomerPassword =>
        Environment.GetEnvironmentVariable("E2E_CUSTOMER_PASSWORD") ?? "Customer@123456!";

    private static bool E2EEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_BASEURL"));

    [SetUp]
    public void RequireE2E()
    {
        Assume.That(E2EEnabled, "E2E_BASEURL not set; skipping Playwright E2E tests.");
    }

    [Test]
    public async Task Home_ShouldLoadAndShowTitle()
    {
        await RunWithPageAsync("home", async page =>
        {
            await TryNavigateWithFallbackAsync(page, BaseUrl);
            var h1 = page.Locator("h1").First;
            await Expect(h1).ToContainTextAsync("Connect Drivers");
        });
    }

    [Test]
    public async Task Admin_Login_ShouldReachDashboard()
    {
        await RunWithPageAsync("admin-login", page => LoginAndAssertAsync(page, AdminEmail, AdminPassword, "**/admin", "Admin Control Center"));
    }

    [Test]
    public async Task Driver_Login_ShouldReachDashboard()
    {
        await RunWithPageAsync("driver-login", page => LoginAndAssertAsync(page, DriverEmail, DriverPassword, "**/dashboard/driver", "Driver Portal"));
    }

    [Test]
    public async Task Customer_Login_ShouldReachDashboard()
    {
        await RunWithPageAsync("customer-login", page => LoginAndAssertAsync(page, CustomerEmail, CustomerPassword, "**/dashboard/customer", "Customer Portal"));
    }

    [Test]
    public async Task Admin_Route_ShouldRedirectToLogin_WhenUnauthenticated()
    {
        await RunWithPageAsync("admin-unauthenticated-redirect", async page =>
        {
            await TryNavigateWithFallbackAsync(page, $"{BaseUrl}/admin");
            await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });
            var loginHeading = page.GetByText("Welcome back");
            await Expect(loginHeading).ToBeVisibleAsync();
        });
    }

    [Test]
    public async Task Login_Fails_WithBadPassword()
    {
        await RunWithPageAsync("login-bad-password", async page =>
        {
            await TryNavigateWithFallbackAsync(page, $"{BaseUrl}/login");
            await page.GetByPlaceholder("you@company.com").FillAsync(AdminEmail);
            await page.GetByPlaceholder("Enter your password").FillAsync("bad-password");
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

            var errorAlert = page.GetByText("Invalid email or password", new PageGetByTextOptions { Exact = false });
            await Expect(errorAlert).ToBeVisibleAsync(new() { Timeout = 5000 });
        });
    }

    private static ILocatorAssertions Expect(ILocator locator) => Microsoft.Playwright.Assertions.Expect(locator);

    private static async Task RunWithPageAsync(string name, Func<IPage, Task> testBody)
    {
        var artifactsDir = Environment.GetEnvironmentVariable("E2E_ARTIFACTS_DIR")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "artifacts", "playwright");
        Directory.CreateDirectory(artifactsDir);

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            RecordVideoDir = artifactsDir,
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
        });

        var tracePath = Path.Combine(artifactsDir, $"trace-{name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid()}.zip");
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true });

        var page = await context.NewPageAsync();

        try
        {
            await testBody(page);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test '{name}' failed: {ex}");
            await CaptureArtifactsAsync(page, context, artifactsDir, tracePath);
            throw;
        }
        finally
        {
            try { await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath }); } catch { }
            try { await context.CloseAsync(); } catch { }
            await Task.Delay(300);
        }
    }

    private static async Task CaptureArtifactsAsync(IPage page, IBrowserContext context, string artifactsDir, string tracePath)
    {
        try
        {
            var screenshotPath = Path.Combine(artifactsDir, $"screenshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid()}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });

            var htmlPath = Path.Combine(artifactsDir, $"page-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid()}.html");
            var content = await page.ContentAsync();
            await File.WriteAllTextAsync(htmlPath, content);

            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
        }
        catch (Exception traceEx)
        {
            Console.WriteLine($"Error capturing artifacts: {traceEx}");
        }
    }

    private static async Task TryNavigateWithFallbackAsync(IPage page, string url, int maxRetries = 2)
    {
        var options = new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.Load };
        var attempt = 0;
        Exception? lastEx = null;
        var triedUrls = new System.Collections.Generic.List<string> { url };

        while (attempt <= maxRetries)
        {
            try
            {
                await page.GotoAsync(url, options);
                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var httpUrl = "http://" + url.Substring("https://".Length);
                    if (!triedUrls.Contains(httpUrl))
                    {
                        triedUrls.Add(httpUrl);
                        url = httpUrl;
                        attempt = 0;
                        continue;
                    }
                }

                attempt++;
                await Task.Delay(1000);
            }
        }

        throw new Exception($"Navigation failed for URLs: {string.Join(",", triedUrls)}", lastEx);
    }

    private static async Task LoginAndAssertAsync(IPage page, string email, string password, string expectedUrlPattern, string expectedHeading)
    {
        await TryNavigateWithFallbackAsync(page, $"{BaseUrl}/login");

        await page.GetByPlaceholder("you@company.com").FillAsync(email);
        await page.GetByPlaceholder("Enter your password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await page.WaitForURLAsync(expectedUrlPattern, new() { Timeout = 20000 });
        var heading = page.GetByText(expectedHeading, new PageGetByTextOptions { Exact = false });
        await Expect(heading).ToBeVisibleAsync(new() { Timeout = 5000 });
    }
}
