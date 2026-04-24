using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Playwright.Browser;

public sealed class PlaywrightBrowserSessionFactory : IBrowserSessionFactory, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private static readonly string InstallScriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");

    public async ValueTask<IBrowserContext> CreateContextAsync(ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(profile.UserDataDir);

        try
        {
            _playwright ??= await Microsoft.Playwright.Playwright.CreateAsync();
        }
        catch (Exception ex) when (ex is PlaywrightException or FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException(
                BuildMissingRuntimeMessage(),
                ex);
        }

        var browserType = _playwright.Chromium;
        var options = new BrowserTypeLaunchPersistentContextOptions
        {
            Channel = profile.Channel == "chromium" ? null : profile.Channel,
            Headless = profile.Headless,
            Locale = profile.Locale,
            IgnoreHTTPSErrors = false
        };

        return await browserType.LaunchPersistentContextAsync(profile.UserDataDir, options);
    }

    public async ValueTask DisposeAsync()
    {
        _playwright?.Dispose();
        await ValueTask.CompletedTask;
    }

    private static string BuildMissingRuntimeMessage()
        => File.Exists(InstallScriptPath)
            ? $"Playwright runtime is not installed. Run `pwsh \"{InstallScriptPath}\" install chromium` and retry."
            : "Playwright runtime is not installed. Rebuild the CLI so Playwright runtime files are copied next to the executable, then run `pwsh <output-dir>/playwright.ps1 install chromium` and retry.";
}
