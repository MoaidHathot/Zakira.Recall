using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Playwright.Providers;

public sealed class DuckDuckGoSearchProvider(HttpClient httpClient) : ISearchProvider
{
    public string Name => "duckduckgo";

    public async ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(request.Query);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
        using var response = await httpClient.GetAsync($"https://html.duckduckgo.com/html/?q={encodedQuery}", timeoutCts.Token);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        return DuckDuckGoHtmlParser.ParseResults(html, request.MaxResults);
    }
}
