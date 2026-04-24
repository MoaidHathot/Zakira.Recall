namespace Zakira.Recall.Abstractions.Services;

public interface ISearchProviderRegistry
{
    ISearchProvider GetRequiredProvider(string providerName);

    IReadOnlyList<string> GetProviderNames();
}
