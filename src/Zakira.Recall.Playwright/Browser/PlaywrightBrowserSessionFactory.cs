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
        PrepareSessionUserDataDir(profile, userDataDir);

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
            IgnoreHTTPSErrors = false,
            ColorScheme = ColorScheme.Light,
            DeviceScaleFactor = 1,
            ViewportSize = new ViewportSize { Width = 1440, Height = 960 },
            UserAgent = BuildUserAgent(profile),
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--lang=en-US"
            ]
        };

        var context = await browserType.LaunchPersistentContextAsync(userDataDir, options);
        await HardenContextAsync(context, cancellationToken);
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

    internal static string? GetSeedUserDataDirForProfile(ProfileDescriptor profile)
        => profile.Headless ? profile.UserDataDir : null;

    private static void PrepareSessionUserDataDir(ProfileDescriptor profile, string sessionUserDataDir)
    {
        var seedUserDataDir = GetSeedUserDataDirForProfile(profile);
        if (string.IsNullOrWhiteSpace(seedUserDataDir) || !Directory.Exists(seedUserDataDir))
        {
            return;
        }

        CopyDirectory(seedUserDataDir, sessionUserDataDir);
    }

    private static async Task HardenContextAsync(IBrowserContext context, CancellationToken cancellationToken)
    {
        await context.AddInitScriptAsync(
            """
            () => {
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined
                });

                Object.defineProperty(navigator, 'languages', {
                    get: () => ['en-US', 'en']
                });

                Object.defineProperty(navigator, 'platform', {
                    get: () => 'Win32'
                });

                window.chrome = window.chrome || { runtime: {} };
            }
            """);
        await Task.CompletedTask.WaitAsync(cancellationToken);
    }

    private static string BuildUserAgent(ProfileDescriptor profile)
    {
        var chromeVersion = profile.Channel switch
        {
            "msedge" => "136.0.0.0",
            "chrome" => "136.0.0.0",
            _ => "136.0.0.0"
        };

        return profile.Channel switch
        {
            "msedge" => $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36 Edg/{chromeVersion}",
            _ => $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36"
        };
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destinationDir, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destinationPath = Path.Combine(destinationDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

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
