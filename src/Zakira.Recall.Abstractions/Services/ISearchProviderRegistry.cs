namespace Zakira.Recall.Abstractions.Services;

public interface ISearchProviderRegistry
{
    ISearchProvider GetRequiredProvider(string providerName);

    bool TryGetProvider(string providerName, out ISearchProvider? provider);

    string? NormalizeProviderName(string? providerName);

    string GetDefaultProviderName();

    IReadOnlyList<ISearchProvider> GetProviders();

    IReadOnlyList<string> GetProviderNames();
}
