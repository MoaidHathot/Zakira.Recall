namespace Zakira.Recall.Playwright.Browser;

/// <summary>
/// Removes throw-away headless session directories. Edge keeps handles open for a moment after a context closes,
/// so deletion is retried in the background; whatever still slips through is swept on the next start.
/// </summary>
internal static class SessionDirectoryCleaner
{
    internal static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// 40 x 250 ms = 10 s. Edge needs about 6.5 s from Close to releasing the last file on a warm machine; the budget
    /// leaves headroom for slower disks without keeping a host alive for long.
    /// </summary>
    internal const int DefaultAttempts = 40;

    /// <summary>Deletes <paramref name="path"/>, retrying while the browser releases its files. Never throws.</summary>
    public static async Task DeleteWithRetryAsync(string path, int attempts = DefaultAttempts, TimeSpan? delay = null)
    {
        var wait = delay ?? DefaultRetryDelay;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            if (TryDelete(path))
            {
                return;
            }

            if (attempt < attempts)
            {
                await Task.Delay(wait);
            }
        }
    }

    /// <summary>
    /// Deletes session directories under <paramref name="root"/> created more than <paramref name="maxAge"/> ago.
    /// Directories still in use by another process fail to delete and are skipped. Returns the number removed.
    /// </summary>
    public static int SweepStale(string root, TimeSpan maxAge, DateTime? nowUtc = null)
    {
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var cutoff = (nowUtc ?? DateTime.UtcNow) - maxAge;
        var removed = 0;
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        foreach (var directory in directories)
        {
            DateTime created;
            try
            {
                created = Directory.GetCreationTimeUtc(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (created <= cutoff && TryDelete(directory))
            {
                removed++;
            }
        }

        return removed;
    }

    internal static bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
