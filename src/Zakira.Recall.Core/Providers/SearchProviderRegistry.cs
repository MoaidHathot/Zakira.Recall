using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Providers;

public sealed class SearchProviderRegistry(IEnumerable<ISearchProvider> providers) : ISearchProviderRegistry
{
    private readonly Dictionary<string, ISearchProvider> _providers = providers.ToDictionary(
        provider => provider.Name,
        StringComparer.OrdinalIgnoreCase);

    public ISearchProvider GetRequiredProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Unknown provider '{providerName}'. Available providers: {string.Join(", ", GetProviderNames())}");
    }

    public IReadOnlyList<string> GetProviderNames() => _providers.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();
}
