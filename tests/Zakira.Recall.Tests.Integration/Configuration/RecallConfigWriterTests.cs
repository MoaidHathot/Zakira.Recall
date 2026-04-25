using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Core.Providers;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Providers;
using Zakira.Recall.Tests.Unit.Infrastructure;

namespace Zakira.Recall.Tests.Integration.Configuration;

public sealed class RecallConfigWriterTests
{
    [Fact]
    public async Task Saves_Config_To_Default_Location_When_Path_Not_Specified()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var environment = new FakeSystemEnvironment(
                new Dictionary<string, string?> { ["XDG_CONFIG_HOME"] = root.FullName },
                @"C:\Roaming",
                @"C:\Local");
            var locator = new RecallConfigLocator(environment);
            var writer = new RecallConfigWriter(locator, new RuntimeDefaults(), new RecallConfigValidator(CreateProviderRegistry()));

            var path = await writer.SaveAsync(new RecallConfig
            {
                DefaultProfile = "personal",
                Profiles = new Dictionary<string, RecallProfileConfig>
                {
                    ["personal"] = new() { DefaultProvider = "duckduckgo" }
                }
            });

            Assert.Equal(Path.Combine(root.FullName, "Zakira.Recall", "profiles.json"), path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Rejects_Invalid_Config_On_Save()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var environment = new FakeSystemEnvironment(
                new Dictionary<string, string?> { ["XDG_CONFIG_HOME"] = root.FullName },
                @"C:\Roaming",
                @"C:\Local");
            var locator = new RecallConfigLocator(environment);
            var writer = new RecallConfigWriter(locator, new RuntimeDefaults(), new RecallConfigValidator(CreateProviderRegistry()));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await writer.SaveAsync(new RecallConfig { DefaultProvider = "google" });
            });
        }
        finally
        {
            root.Delete(true);
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
