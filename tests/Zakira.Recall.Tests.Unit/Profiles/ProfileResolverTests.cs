using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Core.Profiles;
using Zakira.Recall.Core.Providers;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Providers;
using Zakira.Recall.Tests.Unit.Infrastructure;

namespace Zakira.Recall.Tests.Unit.Profiles;

public sealed class ProfileResolverTests
{
    [Fact]
    public async Task Uses_Configured_Profile_Data_Directory_Override()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var appData = Path.Combine(tempRoot.FullName, "app-data");
            var localAppData = Path.Combine(tempRoot.FullName, "local-app-data");
            var configuredUserDataDir = Path.Combine(tempRoot.FullName, "custom", "work-profile");
            var configPath = Path.Combine(tempRoot.FullName, "profiles.json");
            await File.WriteAllTextAsync(configPath, string.Join(Environment.NewLine,
            [
                "{",
                "  \"defaultProfile\": \"work\",",
                "  \"profiles\": {",
                "    \"work\": {",
                "      \"defaultProvider\": \"bing\",",
                $"      \"userDataDir\": \"{configuredUserDataDir.Replace("\\", "\\\\")}\",",
                "      \"channel\": \"msedge\"",
                "    }",
                "  }",
                "}"
            ]));

            var environment = new FakeSystemEnvironment(new Dictionary<string, string?>(), appData, localAppData);
            var runtimeDefaults = new RuntimeDefaults { ConfigPath = configPath };
            IRecallConfigLocator locator = new RecallConfigLocator(environment);
            IRecallConfigValidator validator = new RecallConfigValidator(CreateProviderRegistry());
            IRecallConfigLoader loader = new RecallConfigLoader(locator, runtimeDefaults, validator);
            var resolver = new ProfileResolver(loader, CreateProviderRegistry(), environment);

            var profile = await resolver.ResolveAsync(null, null);

            Assert.Equal("work", profile.Name);
            Assert.Equal("bing", profile.DefaultProvider);
            Assert.Equal(Path.GetFullPath(configuredUserDataDir), profile.UserDataDir);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public async Task Uses_Profiles_Root_From_Runtime_Defaults_When_Present()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var appData = Path.Combine(tempRoot.FullName, "app-data");
            var localAppData = Path.Combine(tempRoot.FullName, "local-app-data");
            var profilesRoot = Path.Combine(tempRoot.FullName, "recall", "profiles");
            var configPath = Path.Combine(tempRoot.FullName, "profiles.json");
            await File.WriteAllTextAsync(configPath, """
            {
              "profiles": {
                "personal": {
                  "defaultProvider": "duckduckgo"
                }
              }
            }
            """);

            var environment = new FakeSystemEnvironment(new Dictionary<string, string?>(), appData, localAppData);
            var runtimeDefaults = new RuntimeDefaults
            {
                ConfigPath = configPath,
                DefaultProfile = "personal",
                ProfilesRoot = profilesRoot
            };

            IRecallConfigLocator locator = new RecallConfigLocator(environment);
            IRecallConfigValidator validator = new RecallConfigValidator(CreateProviderRegistry());
            IRecallConfigLoader loader = new RecallConfigLoader(locator, runtimeDefaults, validator);
            var resolver = new ProfileResolver(loader, CreateProviderRegistry(), environment);

            var profile = await resolver.ResolveAsync("personal", "duckduckgo");

            Assert.Equal(Path.Combine(Path.GetFullPath(profilesRoot), "personal"), profile.UserDataDir);
            Assert.Equal("duckduckgo", profile.DefaultProvider);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public async Task Normalizes_Provider_Aliases_Using_Registry()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var appData = Path.Combine(tempRoot.FullName, "app-data");
            var localAppData = Path.Combine(tempRoot.FullName, "local-app-data");
            var configPath = Path.Combine(tempRoot.FullName, "profiles.json");
            await File.WriteAllTextAsync(configPath, """
            {
              "defaultProvider": "ddg"
            }
            """);

            var environment = new FakeSystemEnvironment(new Dictionary<string, string?>(), appData, localAppData);
            var runtimeDefaults = new RuntimeDefaults { ConfigPath = configPath };
            var registry = CreateProviderRegistry();
            IRecallConfigLocator locator = new RecallConfigLocator(environment);
            IRecallConfigValidator validator = new RecallConfigValidator(registry);
            IRecallConfigLoader loader = new RecallConfigLoader(locator, runtimeDefaults, validator);
            var resolver = new ProfileResolver(loader, registry, environment);

            var profile = await resolver.ResolveAsync(null, null);

            Assert.Equal("duckduckgo", profile.DefaultProvider);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    private static SearchProviderRegistry CreateProviderRegistry()
        => new(
        [
            new DuckDuckGoSearchProvider(new HttpClient(new StubHandler())),
            new DuckDuckGoBrowserSearchProvider(new StubBrowserSessionFactory()),
            new BingSearchProvider(new StubBrowserSessionFactory())
        ]);

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StubBrowserSessionFactory : IBrowserSessionFactory
    {
        public ValueTask<Microsoft.Playwright.IBrowserContext> CreateContextAsync(Zakira.Recall.Abstractions.Models.ProfileDescriptor profile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
