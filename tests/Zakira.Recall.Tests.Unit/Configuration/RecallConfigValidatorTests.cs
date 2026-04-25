using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Configuration;

namespace Zakira.Recall.Tests.Unit.Configuration;

public sealed class RecallConfigValidatorTests
{
    [Fact]
    public void Rejects_Unknown_Default_Provider()
    {
        var validator = new RecallConfigValidator(new FakeProviderRegistry());
        var config = new RecallConfig { DefaultProvider = "google" };

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(config));

        Assert.Contains("Unsupported provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_Invalid_Log_Level()
    {
        var validator = new RecallConfigValidator(new FakeProviderRegistry());
        var config = new RecallConfig { LogLevel = "loud" };

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(config));

        Assert.Contains("Unsupported log level", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_Provider_Aliases_When_Registry_Knows_Them()
    {
        var validator = new RecallConfigValidator(new FakeProviderRegistry());
        var config = new RecallConfig { DefaultProvider = "ddg" };

        var exception = Record.Exception(() => validator.Validate(config));

        Assert.Null(exception);
    }

    private sealed class FakeProviderRegistry : ISearchProviderRegistry
    {
        public ISearchProvider GetRequiredProvider(string providerName)
            => providerName switch
            {
                "duckduckgo" or "ddg" => new FakeProvider(),
                _ => throw new InvalidOperationException()
            };

        public bool TryGetProvider(string providerName, out ISearchProvider? provider)
        {
            provider = NormalizeProviderName(providerName) is null ? null : new FakeProvider();
            return provider is not null;
        }

        public string? NormalizeProviderName(string? providerName)
            => providerName?.Trim().ToLowerInvariant() switch
            {
                "duckduckgo" or "ddg" => "duckduckgo",
                _ => null
            };

        public string GetDefaultProviderName() => "duckduckgo";

        public IReadOnlyList<ISearchProvider> GetProviders() => [new FakeProvider()];

        public IReadOnlyList<string> GetProviderNames() => ["duckduckgo"];
    }

    private sealed class FakeProvider : ISearchProvider
    {
        public string Name => "duckduckgo";

        public IReadOnlyList<string> Aliases => ["ddg"];

        public string? SetupUrl => "https://duckduckgo.com";

        public SearchProviderCapabilities Capabilities => new();

        public ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<SearchResult>>([]);
    }
}
