namespace Zakira.Recall.Playwright.Browser;

/// <summary>
/// Copies the persisted browser profile into a throw-away session directory for headless fetches, leaving out the
/// parts that are pure cache or telemetry. On a typical profile that is ~90% of the bytes (HTTP cache, shader/GPU
/// caches, code cache, SmartScreen, Edge shopping data) while cookies, logins, preferences and storage stay intact.
/// </summary>
internal static class ProfileSeeding
{
    /// <summary>Directory names (any depth) that are never needed to reproduce a signed-in session.</summary>
    internal static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache",
        "GrShaderCache", "ShaderCache", "GraphiteDawnCache", "CacheStorage", "ScriptCache",
        "Crashpad", "BrowserMetrics", "SmartScreen", "EdgeCoupons", "Asset Store", "EntityExtraction", "EdgeJourneys",
        "component_crx_cache", "extensions_crx_cache", "Safe Browsing", "optimization_guide_model_store",
        "blob_storage", "Ad Blocking"
    };

    /// <summary>Files that belong to a running instance and must never be cloned.</summary>
    internal static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "lockfile", "SingletonLock", "SingletonCookie", "SingletonSocket", "DevToolsActivePort", "chrome_debug.log"
    };

    internal static readonly HashSet<string> ExcludedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pma"
    };

    internal readonly record struct CopyStats(int Files, long Bytes);

    public static CopyStats Copy(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        return CopyTree(new DirectoryInfo(sourceDir), destinationDir);
    }

    private static CopyStats CopyTree(DirectoryInfo source, string destinationDir)
    {
        var files = 0;
        var bytes = 0L;

        foreach (var file in source.EnumerateFiles())
        {
            if (ExcludedFileNames.Contains(file.Name) || ExcludedFileExtensions.Contains(file.Extension))
            {
                continue;
            }

            file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite: true);
            files++;
            bytes += file.Length;
        }

        foreach (var directory in source.EnumerateDirectories())
        {
            if (ExcludedDirectoryNames.Contains(directory.Name))
            {
                continue;
            }

            var target = Path.Combine(destinationDir, directory.Name);
            Directory.CreateDirectory(target);
            var nested = CopyTree(directory, target);
            files += nested.Files;
            bytes += nested.Bytes;
        }

        return new CopyStats(files, bytes);
    }
}
