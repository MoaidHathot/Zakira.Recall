using Microsoft.Extensions.DependencyInjection;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Fetch;
using System.Reflection;

namespace Zakira.Recall.Playwright.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRecallPlaywright(this IServiceCollection services)
    {
        services.AddSingleton<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        services.AddSingleton<IPageFetcher, PlaywrightPageFetcher>();
        foreach (var providerType in GetSearchProviderTypes())
        {
            if (RequiresHttpClient(providerType))
            {
                services.AddHttpClient(providerType.FullName ?? providerType.Name);
                services.AddSingleton(typeof(ISearchProvider), serviceProvider =>
                {
                    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                    var parameters = providerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                        .Single()
                        .GetParameters()
                        .Select(parameter => parameter.ParameterType == typeof(HttpClient)
                            ? httpClientFactory.CreateClient(providerType.FullName ?? providerType.Name)
                            : serviceProvider.GetRequiredService(parameter.ParameterType))
                        .ToArray();
                    return (ISearchProvider)Activator.CreateInstance(providerType, parameters)!;
                });
                continue;
            }

            services.AddSingleton(typeof(ISearchProvider), providerType);
        }

        return services;
    }

    private static IReadOnlyList<Type> GetSearchProviderTypes()
        => typeof(ServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: false }
                && typeof(ISearchProvider).IsAssignableFrom(type))
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToArray();

    private static bool RequiresHttpClient(Type providerType)
        => providerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(static constructor => constructor.GetParameters())
            .Any(static parameter => parameter.ParameterType == typeof(HttpClient));
}
