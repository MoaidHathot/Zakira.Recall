using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Playwright.Browser;

public sealed class PlaywrightBrowserSessionFactory : IBrowserSessionFactory, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private static readonly string InstallScriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
    private static readonly string HeadlessSessionsRoot = Path.Combine(Path.GetTempPath(), "Zakira.Recall", "browser-sessions");

    public async ValueTask<IBrowserContext> CreateContextAsync(ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        var userDataDir = ResolveUserDataDir(profile);
        Directory.CreateDirectory(userDataDir);

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

        var context = await browserType.LaunchPersistentContextAsync(userDataDir, options);
        if (profile.Headless)
        {
            context.Close += (_, _) => SafeDeleteDirectory(userDataDir);
        }

        return context;
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

    private static string ResolveUserDataDir(ProfileDescriptor profile)
        => profile.Headless
            ? Path.Combine(HeadlessSessionsRoot, profile.Name, Guid.NewGuid().ToString("N"))
            : profile.UserDataDir;

    internal static string GetUserDataDirForProfile(ProfileDescriptor profile)
        => ResolveUserDataDir(profile);

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
