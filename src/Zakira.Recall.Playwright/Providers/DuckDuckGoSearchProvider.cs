using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Playwright.Providers;

public sealed class DuckDuckGoSearchProvider(HttpClient httpClient) : ISearchProvider
{
    public string Name => "duckduckgo";

    public IReadOnlyList<string> Aliases => ["ddg"];

    public string? SetupUrl => "https://duckduckgo.com";

    public SearchProviderCapabilities Capabilities => new()
    {
        SupportsPagination = true,
        SupportsTimeRange = true,
        SupportsSafeSearch = true,
        RequiresBrowser = false,
        SupportsInteractiveSetup = false
    };

    public async ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
        using var response = await httpClient.GetAsync(BuildSearchUri(request), timeoutCts.Token);

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new HttpRequestException($"DuckDuckGo search returned unexpected status code {(int)response.StatusCode}.", null, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        return DuckDuckGoHtmlParser.ParseResults(html, request.MaxResults);
    }

    private static Uri BuildSearchUri(SearchRequest request)
    {
        var parameters = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}"
        };

        if (request.Page > 1)
        {
            parameters.Add($"s={(request.Page - 1) * Math.Max(1, request.MaxResults)}");
        }

        if (!string.IsNullOrWhiteSpace(request.TimeRange))
        {
            parameters.Add($"df={MapTimeRange(request.TimeRange)}");
        }

        if (request.SafeSearch.HasValue)
        {
            parameters.Add($"kp={(request.SafeSearch.Value ? "1" : "-1")}");
        }

        return new Uri($"https://html.duckduckgo.com/html/?{string.Join("&", parameters)}");
    }

    private static string MapTimeRange(string timeRange)
        => timeRange.Trim().ToLowerInvariant() switch
        {
            "day" => "d",
            "week" => "w",
            "month" => "m",
            "year" => "y",
            _ => timeRange.Trim().ToLowerInvariant()
        };
}
