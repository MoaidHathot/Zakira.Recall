using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Unit.Browser;

public sealed class PlaywrightBrowserSessionFactoryTests
{
    [Fact]
    public void Headless_Profile_Uses_Isolated_Temporary_UserDataDir()
    {
        var profile = new ProfileDescriptor
        {
            Name = "default",
            UserDataDir = @"C:\profiles\default",
            Channel = "msedge",
            Headless = true,
            DefaultProvider = "duckduckgo",
            TimeoutSeconds = 30,
            EnableProviderFallback = true,
            ProviderHealthCooldownSeconds = 300,
            MaxConcurrentFetches = 3
        };

        var first = PlaywrightBrowserSessionFactory.GetUserDataDirForProfile(profile);
        var second = PlaywrightBrowserSessionFactory.GetUserDataDirForProfile(profile);

        Assert.NotEqual(first, second);
        Assert.Contains(Path.Combine("Zakira.Recall", "browser-sessions", profile.Name), first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("Zakira.Recall", "browser-sessions", profile.Name), second, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interactive_Profile_Uses_Persisted_UserDataDir()
    {
        var profile = new ProfileDescriptor
        {
            Name = "interactive",
            UserDataDir = @"C:\profiles\interactive",
            Channel = "msedge",
            Headless = false,
            DefaultProvider = "duckduckgo-browser",
            TimeoutSeconds = 30,
            EnableProviderFallback = true,
            ProviderHealthCooldownSeconds = 300,
            MaxConcurrentFetches = 3
        };

        var userDataDir = PlaywrightBrowserSessionFactory.GetUserDataDirForProfile(profile);

        Assert.Equal(profile.UserDataDir, userDataDir);
    }

    [Fact]
    public void Headless_Profile_Seeds_From_Persisted_Profile_Directory()
    {
        var profile = new ProfileDescriptor
        {
            Name = "default",
            UserDataDir = @"C:\profiles\default",
            Channel = "msedge",
            Headless = true,
            DefaultProvider = "duckduckgo",
            TimeoutSeconds = 30,
            EnableProviderFallback = true,
            ProviderHealthCooldownSeconds = 300,
            MaxConcurrentFetches = 3
        };

        Assert.Equal(profile.UserDataDir, PlaywrightBrowserSessionFactory.GetSeedUserDataDirForProfile(profile));
    }
}
