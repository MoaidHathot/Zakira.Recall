using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Unit.Browser;

public sealed class ProfileSeedingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Zakira.Recall.Tests", "seed-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Copies_Session_State_But_Skips_Caches_Telemetry_And_Lock_Files()
    {
        var source = Path.Combine(_root, "profile");
        var kept = new[]
        {
            "Local State",
            Path.Combine("Default", "Preferences"),
            Path.Combine("Default", "Network", "Cookies"),
            Path.Combine("Default", "Login Data"),
            Path.Combine("Default", "Local Storage", "leveldb", "000003.log"),
            Path.Combine("Default", "IndexedDB", "https_example.com_0.indexeddb.leveldb", "CURRENT"),
            Path.Combine("Default", "Service Worker", "Database", "MANIFEST-000001")
        };
        var dropped = new[]
        {
            "lockfile",
            "BrowserMetrics-spare.pma",
            Path.Combine("Default", "Cache", "Cache_Data", "data_0"),
            Path.Combine("Default", "Code Cache", "js", "index"),
            Path.Combine("Default", "GPUCache", "data_1"),
            Path.Combine("Default", "Service Worker", "CacheStorage", "abc", "index"),
            Path.Combine("GrShaderCache", "data_3"),
            Path.Combine("Default", "EdgeCoupons", "coupons.db"),
            Path.Combine("SmartScreen", "RemoteData", "x.bin"),
            Path.Combine("Crashpad", "settings.dat")
        };
        foreach (var relative in kept.Concat(dropped))
        {
            var path = Path.Combine(source, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, relative);
        }

        var destination = Path.Combine(_root, "session");
        var stats = ProfileSeeding.Copy(source, destination);

        foreach (var relative in kept)
        {
            Assert.True(File.Exists(Path.Combine(destination, relative)), $"expected '{relative}' to be copied");
        }

        foreach (var relative in dropped)
        {
            Assert.False(File.Exists(Path.Combine(destination, relative)), $"expected '{relative}' to be skipped");
        }

        Assert.Equal(kept.Length, stats.Files);
        Assert.Equal(kept.Sum(relative => (long)relative.Length), stats.Bytes);
        Assert.False(Directory.Exists(Path.Combine(destination, "Default", "Cache")));
        Assert.True(Directory.Exists(Path.Combine(destination, "Default", "Service Worker", "Database")));
    }

    [Fact]
    public void Copy_Creates_The_Destination_And_Handles_An_Empty_Profile()
    {
        var source = Path.Combine(_root, "empty");
        Directory.CreateDirectory(source);
        var destination = Path.Combine(_root, "out", "nested");

        var stats = ProfileSeeding.Copy(source, destination);

        Assert.True(Directory.Exists(destination));
        Assert.Equal(new ProfileSeeding.CopyStats(0, 0), stats);
    }
}
