using Microsoft.Extensions.DependencyInjection;
using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Core.Infrastructure;
using Zakira.Recall.Core.Profiles;
using Zakira.Recall.Core.Providers;
using Zakira.Recall.Core.Services;

namespace Zakira.Recall.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRecallCore(this IServiceCollection services)
    {
        services.AddSingleton<RuntimeDefaults>();
        services.AddSingleton<ISystemEnvironment, SystemEnvironment>();
        services.AddSingleton<IRecallConfigLocator, RecallConfigLocator>();
        services.AddSingleton<IRecallConfigLoader, RecallConfigLoader>();
        services.AddSingleton<IRecallConfigWriter, RecallConfigWriter>();
        services.AddSingleton<IProfileResolver, ProfileResolver>();
        services.AddSingleton<IProfileBootstrapper, ProfileBootstrapper>();
        services.AddSingleton<ISearchProviderRegistry, SearchProviderRegistry>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IFetchService, FetchService>();
        services.AddSingleton<IResearchService, ResearchService>();
        return services;
    }
}
