using System.Diagnostics.CodeAnalysis;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Providers;

public sealed class SearchProviderRegistry : ISearchProviderRegistry
{
    private readonly Dictionary<string, ISearchProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISearchProvider> _lookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultProviderName;

    public SearchProviderRegistry(IEnumerable<ISearchProvider> providers)
    {
        foreach (var provider in providers.OrderBy(static provider => provider.Name, StringComparer.OrdinalIgnoreCase))
        {
            AddProvider(provider);
        }

        if (_providers.Count == 0)
        {
            throw new InvalidOperationException("No search providers are registered.");
        }

        _defaultProviderName = _providers.Values
            .Where(static provider => !provider.Capabilities.RequiresBrowser)
            .Select(static provider => provider.Name)
            .FirstOrDefault()
            ?? _providers.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).First();
    }

    public ISearchProvider GetRequiredProvider(string providerName)
    {
        if (TryGetProvider(providerName, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Unknown provider '{providerName}'. Available providers: {string.Join(", ", GetProviderNames())}");
    }

    public bool TryGetProvider(string providerName, [NotNullWhen(true)] out ISearchProvider? provider)
    {
        provider = null;
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return false;
        }

        return _lookup.TryGetValue(providerName.Trim(), out provider);
    }

    public string? NormalizeProviderName(string? providerName)
        => string.IsNullOrWhiteSpace(providerName)
            ? null
            : TryGetProvider(providerName, out var provider)
                ? provider.Name
                : null;

    public string GetDefaultProviderName() => _defaultProviderName;

    public IReadOnlyList<ISearchProvider> GetProviders() => _providers.Values.OrderBy(static provider => provider.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> GetProviderNames() => _providers.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    private void AddProvider(ISearchProvider provider)
    {
        if (_providers.ContainsKey(provider.Name))
        {
            throw new InvalidOperationException($"Duplicate provider registration for '{provider.Name}'.");
        }

        _providers.Add(provider.Name, provider);
        AddLookupKey(provider.Name, provider);
        foreach (var alias in provider.Aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                AddLookupKey(alias, provider);
            }
        }
    }

    private void AddLookupKey(string key, ISearchProvider provider)
    {
        if (_lookup.TryGetValue(key, out var existing) && !ReferenceEquals(existing, provider))
        {
            throw new InvalidOperationException($"Provider alias '{key}' is already registered by '{existing.Name}'.");
        }

        _lookup[key] = provider;
    }
}
