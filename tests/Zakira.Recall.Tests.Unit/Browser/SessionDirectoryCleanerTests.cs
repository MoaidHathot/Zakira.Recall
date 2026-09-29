using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Unit.Browser;

public sealed class SessionDirectoryCleanerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Zakira.Recall.Tests", "sessions-" + Guid.NewGuid().ToString("N"));

    public SessionDirectoryCleanerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateSession(string name, TimeSpan age)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "Preferences"), "{}");
        Directory.SetCreationTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    [Fact]
    public void Sweep_Removes_Old_Sessions_Keeps_Recent_Ones_And_Skips_Directories_In_Use()
    {
        var stale = CreateSession("stale", TimeSpan.FromHours(3));
        var recent = CreateSession("recent", TimeSpan.FromMinutes(5));
        var inUse = CreateSession("in-use", TimeSpan.FromHours(3));

        int removed;
        using (new FileStream(Path.Combine(inUse, "Preferences"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            removed = SessionDirectoryCleaner.SweepStale(_root, TimeSpan.FromHours(1));
        }

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(recent));
        Assert.True(Directory.Exists(inUse));
    }

    [Fact]
    public void Sweep_Is_A_No_Op_For_A_Missing_Root()
    {
        Assert.Equal(0, SessionDirectoryCleaner.SweepStale(Path.Combine(_root, "does-not-exist"), TimeSpan.Zero));
    }

    [Fact]
    public async Task Delete_Retries_Until_The_Browser_Releases_Its_Files()
    {
        var session = CreateSession("closing", TimeSpan.Zero);
        var file = Path.Combine(session, "Preferences");

        Task deletion;
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            deletion = SessionDirectoryCleaner.DeleteWithRetryAsync(session, attempts: 40, delay: TimeSpan.FromMilliseconds(50));
            await Task.Delay(200);
            Assert.True(Directory.Exists(session)); // still locked
        }

        await deletion;
        Assert.False(Directory.Exists(session));
    }

    [Fact]
    public async Task Delete_Gives_Up_Quietly_When_The_Directory_Stays_Locked()
    {
        var session = CreateSession("stuck", TimeSpan.Zero);

        using (new FileStream(Path.Combine(session, "Preferences"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await SessionDirectoryCleaner.DeleteWithRetryAsync(session, attempts: 3, delay: TimeSpan.FromMilliseconds(10));
            Assert.True(Directory.Exists(session));
        }
    }
}
