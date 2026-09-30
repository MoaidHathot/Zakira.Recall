using System.Collections.Concurrent;
using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Playwright.Browser;

public sealed class PlaywrightBrowserSessionFactory : IBrowserSessionFactory, IAsyncDisposable
{
    private static readonly string InstallScriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
    private static readonly string HeadlessSessionsRoot = Path.Combine(Path.GetTempPath(), "Zakira.Recall", "browser-sessions");

    /// <summary>Session directories older than this cannot belong to a live fetch and are swept at start-up.</summary>
    internal static readonly TimeSpan StaleSessionAge = TimeSpan.FromHours(1);

    /// <summary>
    /// How long disposal waits for background directory cleanup before letting the process exit. Edge takes about
    /// 6.5 s from Close to releasing its last file; the wait must cover that or short-lived CLI runs leak the directory.
    /// </summary>
    internal static readonly TimeSpan CleanupDrainTimeout = TimeSpan.FromSeconds(12);

    private readonly ConcurrentDictionary<string, bool> _sweptProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Task, byte> _pendingCleanups = new();

    /// <summary>
    /// Headless session directories created by this factory that have not been deleted yet. When the driver dies,
    /// every browser it launched dies with it without the contexts ever reporting Close, so these are the directories
    /// that would otherwise linger until the stale sweep an hour later.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _openSessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per browser channel: the user agent to present (null when the browser's own string needs no change).
    /// Learned from the first headless launch, see <see cref="BrowserIdentity"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _userAgentOverrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The Playwright driver is a Node child process shared by every fetch in this process. If it dies (crash, or an
    /// external "kill node" sweep) its connection stays closed forever, so it is replaced instead of being cached blindly.
    /// </summary>
    private readonly DriverHandle<IPlaywright> _driver;

    public PlaywrightBrowserSessionFactory()
    {
        _driver = new DriverHandle<IPlaywright>(CreateDriverAsync, static driver => driver.Dispose());
        _driver.DriverLost += CleanUpOpenSessions;
    }

    /// <summary>Number of driver processes started so far (diagnostics).</summary>
    public int DriverCreationCount => _driver.CreationCount;

    /// <summary>Number of headless session directories created by this factory that still exist (diagnostics).</summary>
    public int OpenSessionCount => _openSessions.Count;

    public async ValueTask<IBrowserContext> CreateContextAsync(ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        SweepStaleSessionsOnce(profile);
        var userDataDir = PrepareSessionDirectory(profile);

        var options = BuildLaunchOptions(profile, GetKnownUserAgent(profile));
        var context = await LaunchAsync(profile, userDataDir, options, cancellationToken);

        if (profile.Headless && !_userAgentOverrides.ContainsKey(profile.Channel))
        {
            // First headless launch for this channel: learn the browser's own user agent and, if it carries the
            // headless marker, relaunch once with the regular form. Later launches use the cached value directly.
            var reported = await ReadUserAgentAsync(context);
            var normalized = BrowserIdentity.NormalizeUserAgent(reported);
            _userAgentOverrides.TryAdd(profile.Channel, normalized);
            if (normalized is not null)
            {
                ScheduleSessionCleanup(context, userDataDir);
                await context.CloseAsync();
                userDataDir = PrepareSessionDirectory(profile);
                options.UserAgent = normalized;
                context = await LaunchAsync(profile, userDataDir, options, cancellationToken);
            }
        }

        await HardenContextAsync(context, cancellationToken);
        if (profile.Headless)
        {
            ScheduleSessionCleanup(context, userDataDir);
        }

        return context;
    }

    /// <summary>
    /// Deletes the session directory once the browser process behind the context has exited. Deletion is attempted
    /// from the moment the context closes and then every retry interval; the browser's Disconnected event only wakes
    /// the loop early. Waiting for the event alone is not enough: when the driver process dies, the event never
    /// arrives although the browser is gone and the directory is free.
    /// </summary>
    private void ScheduleSessionCleanup(IBrowserContext context, string sessionDir)
    {
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (context.Browser is { } browser)
        {
            browser.Disconnected += (_, _) => exited.TrySetResult();
        }

        context.Close += (_, _) => TrackCleanup(DeleteWhenReleasedAsync(exited.Task, sessionDir));
    }

    /// <summary>
    /// The driver died, taking every browser it launched with it: contexts that were still open never report Close,
    /// so their directories are released here. Runs for each session directory this factory still knows about.
    /// </summary>
    private void CleanUpOpenSessions()
    {
        foreach (var sessionDir in _openSessions.Keys)
        {
            TrackCleanup(DeleteWhenReleasedAsync(Task.CompletedTask, sessionDir));
        }
    }

    private async Task DeleteWhenReleasedAsync(Task browserExited, string sessionDir)
    {
        var delay = SessionDirectoryCleaner.DefaultRetryDelay;
        var attempt = 0;
        for (; attempt < SessionDirectoryCleaner.DefaultAttempts; attempt++)
        {
            if (SessionDirectoryCleaner.TryDelete(sessionDir))
            {
                break;
            }

            // Poll while the browser shuts down; return early from the wait as soon as it reports disconnection.
            await (browserExited.IsCompleted ? Task.Delay(delay) : Task.WhenAny(browserExited, Task.Delay(delay)));
        }

        if (attempt == SessionDirectoryCleaner.DefaultAttempts)
        {
            return; // still held; the stale sweep on the next start takes it
        }

        // A browser that is still shutting down can write one last file (Edge: BookmarkMergedSurfaceOrdering) and
        // recreate the directory after it was deleted. Wait for it to report disconnection, then delete once more.
        var remaining = delay * (SessionDirectoryCleaner.DefaultAttempts - attempt);
        await (browserExited.IsCompleted ? Task.Delay(delay * 4) : Task.WhenAny(browserExited, Task.Delay(remaining)));
        SessionDirectoryCleaner.TryDelete(sessionDir);
        _openSessions.TryRemove(sessionDir, out _);
    }

    /// <summary>Headless fetches get a fresh, cache-free copy of the profile; interactive ones use the profile itself.</summary>
    private static string PrepareSessionDirectory(ProfileDescriptor profile)
    {
        var userDataDir = ResolveUserDataDir(profile);
        Directory.CreateDirectory(userDataDir);
        PrepareSessionUserDataDir(profile, userDataDir);
        return userDataDir;
    }

    private string? GetKnownUserAgent(ProfileDescriptor profile)
        => profile.Headless && _userAgentOverrides.TryGetValue(profile.Channel, out var userAgent) ? userAgent : null;

    private static BrowserTypeLaunchPersistentContextOptions BuildLaunchOptions(ProfileDescriptor profile, string? userAgent)
        => new()
        {
            Channel = profile.Channel == "chromium" ? null : profile.Channel,
            Headless = profile.Headless,
            Locale = profile.Locale,
            UserAgent = userAgent,
            IgnoreHTTPSErrors = false,
            ColorScheme = ColorScheme.Light,
            DeviceScaleFactor = 1,
            ViewportSize = new ViewportSize { Width = 1440, Height = 960 },
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--lang=en-US"
            ]
        };

    private async Task<IBrowserContext> LaunchAsync(ProfileDescriptor profile, string userDataDir, BrowserTypeLaunchPersistentContextOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var context = await _driver.RunAsync(
                playwright => playwright.Chromium.LaunchPersistentContextAsync(userDataDir, options),
                PlaywrightErrors.IsDriverProcessExited,
                cancellationToken);

            // Tracked only once a browser actually owns the directory. A launch that is being retried on a fresh
            // driver must not be swept by the DriverLost handler in between.
            if (profile.Headless)
            {
                _openSessions.TryAdd(userDataDir, 0);
            }

            return context;
        }
        catch (PlaywrightException ex) when (PlaywrightErrors.IsMissingBrowserExecutable(ex))
        {
            ReleaseUnlaunchedSession(profile, userDataDir);
            throw new InvalidOperationException(BuildMissingBrowserMessage(profile), ex);
        }
        catch
        {
            ReleaseUnlaunchedSession(profile, userDataDir);
            throw;
        }
    }

    /// <summary>A headless session directory whose browser never started has no owner; remove it right away.</summary>
    private void ReleaseUnlaunchedSession(ProfileDescriptor profile, string userDataDir)
    {
        if (profile.Headless)
        {
            TrackCleanup(DeleteWhenReleasedAsync(Task.CompletedTask, userDataDir));
        }
    }

    /// <summary>Reads navigator.userAgent from the initial blank page; no navigation, no network.</summary>
    private static async Task<string?> ReadUserAgentAsync(IBrowserContext context)
    {
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        return await page.EvaluateAsync<string>("() => navigator.userAgent");
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();

        // Disposing the driver closes its browsers; whatever is still tracked has no context left to report Close.
        CleanUpOpenSessions();

        // Short-lived hosts (the CLI) exit right after a fetch; give directory cleanup a bounded chance to finish.
        var pending = _pendingCleanups.Keys.ToArray();
        if (pending.Length > 0)
        {
            await Task.WhenAny(Task.WhenAll(pending), Task.Delay(CleanupDrainTimeout));
        }
    }

    private void TrackCleanup(Task cleanup)
    {
        if (cleanup.IsCompleted)
        {
            return;
        }

        _pendingCleanups.TryAdd(cleanup, 0);
        cleanup.ContinueWith(task => _pendingCleanups.TryRemove(task, out _), TaskScheduler.Default);
    }

    private static async Task<IPlaywright> CreateDriverAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Microsoft.Playwright.Playwright.CreateAsync();
        }
        catch (Exception ex) when (ex is PlaywrightException or FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException(BuildMissingRuntimeMessage(), ex);
        }
    }

    private static string BuildMissingRuntimeMessage()
        => File.Exists(InstallScriptPath)
            ? $"Playwright runtime is not installed. Run `pwsh \"{InstallScriptPath}\" install chromium` and retry."
            : "Playwright runtime is not installed. Rebuild the CLI so Playwright runtime files are copied next to the executable, then run `pwsh <output-dir>/playwright.ps1 install chromium` and retry.";

    private static string BuildMissingBrowserMessage(ProfileDescriptor profile)
        => profile.Channel == "chromium"
            ? $"The Playwright Chromium browser is not installed. Run `pwsh \"{InstallScriptPath}\" install chromium` and retry."
            : $"Browser channel '{profile.Channel}' is not installed on this machine. Install it, or set the profile channel to \"chromium\" and run `pwsh \"{InstallScriptPath}\" install chromium`.";

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

        ProfileSeeding.Copy(seedUserDataDir, sessionUserDataDir);
    }

    /// <summary>Once per process and profile, remove session directories left behind by earlier runs.</summary>
    private void SweepStaleSessionsOnce(ProfileDescriptor profile)
    {
        if (!profile.Headless || !_sweptProfiles.TryAdd(profile.Name, true))
        {
            return;
        }

        var root = Path.Combine(HeadlessSessionsRoot, profile.Name);
        TrackCleanup(Task.Run(() => SessionDirectoryCleaner.SweepStale(root, StaleSessionAge)));
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
}
