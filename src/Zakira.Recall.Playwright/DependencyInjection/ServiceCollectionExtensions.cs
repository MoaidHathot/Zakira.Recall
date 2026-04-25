using Microsoft.Extensions.DependencyInjection;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Fetch;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Playwright.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRecallPlaywright(this IServiceCollection services)
    {
        services.AddSingleton<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        services.AddHttpClient<DuckDuckGoSearchProvider>();
        services.AddSingleton<IPageFetcher, PlaywrightPageFetcher>();
        services.AddSingleton<ISearchProvider, DuckDuckGoSearchProvider>();
        services.AddSingleton<ISearchProvider, DuckDuckGoBrowserSearchProvider>();
        services.AddSingleton<ISearchProvider, BingSearchProvider>();
        return services;
    }
}
