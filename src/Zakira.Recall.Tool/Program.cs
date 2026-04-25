using System.CommandLine;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Dumpify;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.DependencyInjection;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.DependencyInjection;

return await ZakiraRecallProgram.RunAsync(args);

internal static class ZakiraRecallProgram
{
    public static Task<int> RunAsync(string[] args)
    {
        var configuration = new CommandLineConfiguration(BuildRootCommand());
        return configuration.InvokeAsync(args);
    }

    private static RootCommand BuildRootCommand()
    {
        var configOption = CreateStringOption("--config", "Path to the recall config file.", recursive: true);
        var defaultProviderOption = CreateStringOption("--default-provider", "Default provider override.", recursive: true);
        var defaultProfileOption = CreateStringOption("--default-profile", "Default profile override.", recursive: true);
        var profilesRootOption = CreateStringOption("--profiles-root", "Override the profiles root directory.", recursive: true);
        var logLevelOption = CreateStringOption("--log-level", "Logging level override.", recursive: true);

        var root = new RootCommand("Local CLI and MCP server for web search, fetch, and research workflows.");
        root.Add(configOption);
        root.Add(defaultProviderOption);
        root.Add(defaultProfileOption);
        root.Add(profilesRootOption);
        root.Add(logLevelOption);
        root.Add(BuildSearchCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        root.Add(BuildFetchCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        root.Add(BuildResearchCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        root.Add(BuildProvidersCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        root.Add(BuildConfigCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        root.Add(BuildProfileCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        root.Add(BuildMcpCommand(configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
        return root;
    }

    private static Command BuildSearchCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("search", "Search the web.");
        var queryArgument = CreateRequiredArgument<string>("query", "The raw search query.");
        var providerOption = CreateStringOption("--provider", "Provider override.");
        var profileOption = CreateStringOption("--profile", "Profile name.");
        var limitOption = CreateIntOption("--limit", "Maximum number of results.");
        var pageOption = CreateIntOption("--page", "Result page number.");
        var timeRangeOption = CreateStringOption("--time-range", "Optional time range such as day, week, month, or year.");
        var safeSearchOption = CreateStringOption("--safe-search", "Safe search override: true or false.");
        var fallbackOption = CreateStringOption("--fallback", "Provider fallback override: true or false.");
        var fallbackProvidersOption = CreateStringListOption("--fallback-provider", "Fallback provider name.");
        var outputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        command.Add(queryArgument);
        command.Add(providerOption);
        command.Add(profileOption);
        command.Add(limitOption);
        command.Add(pageOption);
        command.Add(timeRangeOption);
        command.Add(safeSearchOption);
        command.Add(fallbackOption);
        command.Add(fallbackProvidersOption);
        command.Add(outputOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var service = host.Services.GetRequiredService<ISearchService>();
            var response = await service.SearchAsync(new SearchRequest
            {
                Query = parseResult.GetValue(queryArgument)!,
                Provider = parseResult.GetValue(providerOption),
                Profile = parseResult.GetValue(profileOption),
                MaxResults = parseResult.GetValue(limitOption) ?? 10,
                Page = parseResult.GetValue(pageOption) ?? 1,
                TimeRange = parseResult.GetValue(timeRangeOption),
                SafeSearch = ParseNullableBool(parseResult.GetValue(safeSearchOption), "--safe-search"),
                EnableFallback = ParseNullableBool(parseResult.GetValue(fallbackOption), "--fallback"),
                FallbackProviders = parseResult.GetValue(fallbackProvidersOption) ?? []
            }, cancellationToken);

            WriteOutput(response, parseResult.GetValue(outputOption), "text");
            return response.Error is null ? 0 : 1;
        });
        return command;
    }

    private static Command BuildFetchCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("fetch", "Fetch readable content from a URL.");
        var urlArgument = CreateRequiredArgument<string>("url", "The URL to fetch.");
        var profileOption = CreateStringOption("--profile", "Profile name.");
        var timeoutOption = CreateIntOption("--timeout", "Navigation timeout in seconds.");
        var outputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        command.Add(urlArgument);
        command.Add(profileOption);
        command.Add(timeoutOption);
        command.Add(outputOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var service = host.Services.GetRequiredService<IFetchService>();
            var response = await service.FetchAsync(new FetchRequest
            {
                Url = parseResult.GetValue(urlArgument)!,
                Profile = parseResult.GetValue(profileOption),
                TimeoutSeconds = parseResult.GetValue(timeoutOption) ?? 30
            }, cancellationToken);

            WriteOutput(response, parseResult.GetValue(outputOption), "text");
            return response.Success ? 0 : 1;
        });
        return command;
    }

    private static Command BuildResearchCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("research", "Search and fetch top result pages.");
        var queryArgument = CreateRequiredArgument<string>("query", "The raw research query.");
        var providerOption = CreateStringOption("--provider", "Provider override.");
        var profileOption = CreateStringOption("--profile", "Profile name.");
        var limitOption = CreateIntOption("--limit", "Maximum number of search results.");
        var topPagesOption = CreateIntOption("--top-pages", "Number of top pages to fetch.");
        var pageOption = CreateIntOption("--page", "Result page number.");
        var timeRangeOption = CreateStringOption("--time-range", "Optional time range such as day, week, month, or year.");
        var safeSearchOption = CreateStringOption("--safe-search", "Safe search override: true or false.");
        var fallbackOption = CreateStringOption("--fallback", "Provider fallback override: true or false.");
        var fallbackProvidersOption = CreateStringListOption("--fallback-provider", "Fallback provider name.");
        var concurrencyOption = CreateIntOption("--max-concurrent-fetches", "Maximum number of parallel fetches.");
        var domainDiversityOption = CreateStringOption("--domain-diversity", "Prefer unique domains when selecting pages to fetch: true or false.");
        var outputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        command.Add(queryArgument);
        command.Add(providerOption);
        command.Add(profileOption);
        command.Add(limitOption);
        command.Add(topPagesOption);
        command.Add(pageOption);
        command.Add(timeRangeOption);
        command.Add(safeSearchOption);
        command.Add(fallbackOption);
        command.Add(fallbackProvidersOption);
        command.Add(concurrencyOption);
        command.Add(domainDiversityOption);
        command.Add(outputOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var service = host.Services.GetRequiredService<IResearchService>();
            var response = await service.ResearchAsync(new ResearchRequest
            {
                Query = parseResult.GetValue(queryArgument)!,
                Provider = parseResult.GetValue(providerOption),
                Profile = parseResult.GetValue(profileOption),
                MaxResults = parseResult.GetValue(limitOption) ?? 8,
                TopPagesToRead = parseResult.GetValue(topPagesOption) ?? 3,
                Page = parseResult.GetValue(pageOption) ?? 1,
                TimeRange = parseResult.GetValue(timeRangeOption),
                SafeSearch = ParseNullableBool(parseResult.GetValue(safeSearchOption), "--safe-search"),
                EnableFallback = ParseNullableBool(parseResult.GetValue(fallbackOption), "--fallback"),
                FallbackProviders = parseResult.GetValue(fallbackProvidersOption) ?? [],
                MaxConcurrentFetches = parseResult.GetValue(concurrencyOption),
                EnforceDomainDiversity = ParseNullableBool(parseResult.GetValue(domainDiversityOption), "--domain-diversity") ?? true
            }, cancellationToken);

            WriteOutput(response, parseResult.GetValue(outputOption), "text");
            return response.Errors.Count == 0 ? 0 : 1;
        });
        return command;
    }

    private static Command BuildProvidersCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("providers", "Provider capability discovery.");
        var listCommand = new Command("list", "List available providers and health state.");
        var testCommand = new Command("test", "Validate a provider and show its resolved capabilities.");
        var profileOption = CreateStringOption("--profile", "Profile name.");
        var outputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");
        var testProviderArgument = CreateRequiredArgument<string>("name", "Provider name or alias.");
        var testOutputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        listCommand.Add(profileOption);
        listCommand.Add(outputOption);
        listCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var registry = host.Services.GetRequiredService<ISearchProviderRegistry>();
            var healthTracker = host.Services.GetRequiredService<IProviderHealthTracker>();
            var resolver = host.Services.GetRequiredService<IProfileResolver>();
            var profile = await resolver.ResolveAsync(parseResult.GetValue(profileOption), providerOverride: null, cancellationToken);
            var providers = registry.GetProviders()
                .Select(provider => new SearchProviderDescriptor
                {
                    Name = provider.Name,
                    Aliases = provider.Aliases,
                    SetupUrl = provider.SetupUrl,
                    Capabilities = provider.Capabilities,
                    Health = healthTracker.GetSnapshot(provider.Name, profile.ProviderHealthCooldownSeconds)
                })
                .ToArray();

            WriteOutput(providers, parseResult.GetValue(outputOption), "text");
            return 0;
        });

        testCommand.Add(testProviderArgument);
        testCommand.Add(profileOption);
        testCommand.Add(testOutputOption);
        testCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var registry = host.Services.GetRequiredService<ISearchProviderRegistry>();
            var healthTracker = host.Services.GetRequiredService<IProviderHealthTracker>();
            var resolver = host.Services.GetRequiredService<IProfileResolver>();
            var profile = await resolver.ResolveAsync(parseResult.GetValue(profileOption), providerOverride: null, cancellationToken);
            var provider = registry.GetRequiredProvider(parseResult.GetValue(testProviderArgument)!);
            var descriptor = new SearchProviderDescriptor
            {
                Name = provider.Name,
                Aliases = provider.Aliases,
                SetupUrl = provider.SetupUrl,
                Capabilities = provider.Capabilities,
                Health = healthTracker.GetSnapshot(provider.Name, profile.ProviderHealthCooldownSeconds)
            };

            WriteOutput(descriptor, parseResult.GetValue(testOutputOption), "text");
            return 0;
        });

        command.Add(listCommand);
        command.Add(testCommand);
        return command;
    }

    private static Command BuildConfigCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("config", "Manage configuration.");
        var initCommand = new Command("init", "Write an initial config file.");
        var showCommand = new Command("show", "Show the resolved configuration.");
        var pathOption = CreateStringOption("--path", "Output config path.");
        var profileOption = CreateStringOption("--profile", "Default profile name.");
        var interactiveProfileOption = CreateStringOption("--interactive-profile", "Interactive profile name.");
        var providerOption = CreateStringOption("--provider", "Default provider.");
        var channelOption = CreateStringOption("--channel", "Browser channel.");
        var localeOption = CreateStringOption("--locale", "Browser locale.");
        var initProfilesRootOption = CreateStringOption("--profiles-root", "Profiles root directory.");
        var fallbackProvidersOption = CreateStringListOption("--fallback-provider", "Fallback provider name.");
        var enableFallbackOption = CreateStringOption("--enable-fallback", "Enable provider fallback: true or false.");
        var cooldownOption = CreateIntOption("--provider-health-cooldown", "Provider failure cooldown in seconds.");
        var concurrencyOption = CreateIntOption("--max-concurrent-fetches", "Maximum number of parallel page fetches.");
        var configLogLevelOption = CreateStringOption("--config-log-level", "Default config log level.");
        var outputOption = CreateStringOption("--output", "Output mode: text or json.");
        var showPathOption = CreateStringOption("--path", "Explicit config path.");
        var showOutputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        initCommand.Add(pathOption);
        initCommand.Add(profileOption);
        initCommand.Add(interactiveProfileOption);
        initCommand.Add(providerOption);
        initCommand.Add(channelOption);
        initCommand.Add(localeOption);
        initCommand.Add(initProfilesRootOption);
        initCommand.Add(fallbackProvidersOption);
        initCommand.Add(enableFallbackOption);
        initCommand.Add(cooldownOption);
        initCommand.Add(concurrencyOption);
        initCommand.Add(configLogLevelOption);
        initCommand.Add(outputOption);
        initCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var writer = host.Services.GetRequiredService<IRecallConfigWriter>();
            var locator = host.Services.GetRequiredService<IRecallConfigLocator>();
            using var validationHost = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, profileOption));
            var registry = validationHost.Services.GetRequiredService<ISearchProviderRegistry>();
            var provider = registry.NormalizeProviderName(parseResult.GetValue(providerOption)) ?? registry.GetDefaultProviderName();
            var profileName = parseResult.GetValue(profileOption) ?? "default";
            var interactiveProfileName = parseResult.GetValue(interactiveProfileOption) ?? "interactive";
            var fallbackProviders = (parseResult.GetValue(fallbackProvidersOption) ?? [])
                .Select(registry.NormalizeProviderName)
                .Where(static providerName => !string.IsNullOrWhiteSpace(providerName))
                .Cast<string>()
                .Where(providerName => !string.Equals(providerName, provider, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var enableFallback = ParseNullableBool(parseResult.GetValue(enableFallbackOption), "--enable-fallback") ?? true;
            var channel = parseResult.GetValue(channelOption) ?? "msedge";
            var locale = parseResult.GetValue(localeOption) ?? "en-US";
            var configLogLevel = parseResult.GetValue(configLogLevelOption) ?? "Information";

            var config = new RecallConfig
            {
                DefaultProvider = provider,
                DefaultProfile = profileName,
                ProfilesRoot = parseResult.GetValue(initProfilesRootOption),
                FallbackProviders = fallbackProviders,
                EnableProviderFallback = enableFallback,
                ProviderHealthCooldownSeconds = parseResult.GetValue(cooldownOption) ?? 300,
                MaxConcurrentFetches = parseResult.GetValue(concurrencyOption) ?? 3,
                LogLevel = configLogLevel,
                Profiles = new Dictionary<string, RecallProfileConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    [profileName] = new()
                    {
                        Name = profileName,
                        Channel = channel,
                        DefaultProvider = provider,
                        Headless = true,
                        Locale = locale,
                        TimeoutSeconds = 30,
                        FallbackProviders = fallbackProviders,
                        EnableProviderFallback = enableFallback,
                        ProviderHealthCooldownSeconds = parseResult.GetValue(cooldownOption) ?? 300,
                        MaxConcurrentFetches = parseResult.GetValue(concurrencyOption) ?? 3,
                        LogLevel = configLogLevel,
                        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    },
                    [interactiveProfileName] = new()
                    {
                        Name = interactiveProfileName,
                        Channel = channel,
                        DefaultProvider = provider,
                        Headless = false,
                        TimeoutSeconds = 45,
                        FallbackProviders = fallbackProviders,
                        EnableProviderFallback = enableFallback,
                        ProviderHealthCooldownSeconds = parseResult.GetValue(cooldownOption) ?? 300,
                        MaxConcurrentFetches = parseResult.GetValue(concurrencyOption) ?? 3,
                        LogLevel = configLogLevel,
                        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["purpose"] = "manual sign-in and browsing"
                        }
                    }
                }
            };

            var path = parseResult.GetValue(pathOption) ?? locator.GetDefaultConfigPath();
            var savedPath = await writer.SaveAsync(config, path, cancellationToken);
            var outputMode = NormalizeOutputMode(parseResult.GetValue(outputOption), "text");
            if (outputMode == "json")
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new { path = savedPath }, JsonSupport.Options));
            }
            else
            {
                Console.Out.WriteLine(savedPath);
            }

            return 0;
        });

        showCommand.Add(showPathOption);
        showCommand.Add(showOutputOption);
        showCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
            var loader = host.Services.GetRequiredService<IRecallConfigLoader>();
            var config = await loader.LoadAsync(parseResult.GetValue(showPathOption), cancellationToken);
            WriteOutput(config, parseResult.GetValue(showOutputOption), "json");
            return 0;
        });

        command.Add(initCommand);
        command.Add(showCommand);
        return command;
    }

    private static Command BuildProfileCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("profile", "Manage browser profiles.");

        var initCommand = new Command("init", "Create or update a profile.");
        var initNameArgument = CreateRequiredArgument<string>("name", "Profile name.");
        var initChannelOption = CreateStringOption("--channel", "Browser channel.");
        var initProviderOption = CreateStringOption("--provider", "Default provider.");
        var initHeadlessOption = CreateStringOption("--headless", "Profile headless setting: true or false.");
        var initUserDataDirOption = CreateStringOption("--user-data-dir", "User data directory.");
        var initFallbackProvidersOption = CreateStringListOption("--fallback-provider", "Fallback provider name.");
        var initFallbackOption = CreateStringOption("--fallback", "Provider fallback override: true or false.");
        var initCooldownOption = CreateIntOption("--provider-health-cooldown", "Provider failure cooldown in seconds.");
        var initConcurrencyOption = CreateIntOption("--max-concurrent-fetches", "Maximum number of parallel page fetches.");
        var initProfileLogLevelOption = CreateStringOption("--profile-log-level", "Profile-specific log level.");
        var initOutputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        initCommand.Add(initNameArgument);
        initCommand.Add(initChannelOption);
        initCommand.Add(initProviderOption);
        initCommand.Add(initHeadlessOption);
        initCommand.Add(initUserDataDirOption);
        initCommand.Add(initFallbackProvidersOption);
        initCommand.Add(initFallbackOption);
        initCommand.Add(initCooldownOption);
        initCommand.Add(initConcurrencyOption);
        initCommand.Add(initProfileLogLevelOption);
        initCommand.Add(initOutputOption);
        initCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption));
            var bootstrapper = host.Services.GetRequiredService<IProfileBootstrapper>();
            var profile = await bootstrapper.EnsureProfileAsync(
                parseResult.GetValue(initNameArgument)!,
                parseResult.GetValue(initChannelOption) ?? "msedge",
                parseResult.GetValue(initProviderOption) ?? "duckduckgo",
                ParseNullableBool(parseResult.GetValue(initHeadlessOption), "--headless"),
                parseResult.GetValue(initUserDataDirOption),
                parseResult.GetValue(initFallbackProvidersOption) ?? [],
                ParseNullableBool(parseResult.GetValue(initFallbackOption), "--fallback"),
                parseResult.GetValue(initCooldownOption),
                parseResult.GetValue(initConcurrencyOption),
                parseResult.GetValue(initProfileLogLevelOption),
                cancellationToken);

            Directory.CreateDirectory(profile.UserDataDir);
            WriteOutput(profile, parseResult.GetValue(initOutputOption), "text");
            return 0;
        });

        var authCommand = new Command("auth", "Launch an interactive browser session for manual sign-in.");
        var authNameArgument = CreateOptionalArgument<string?>("name", "Profile name.");
        var authProviderOption = CreateStringOption("--provider", "Provider override.");
        var authUrlOption = CreateStringOption("--url", "Optional URL to open for manual setup.");
        var authOutputOption = CreateStringOption("--output", "Output mode: text, json, markdown, or dump.");
        var authNoWaitOption = CreateStringOption("--no-wait", "Close immediately after navigation without waiting for Enter: true or false.");
        var showCommand = new Command("show", "Show a resolved profile.");
        var showNameArgument = CreateOptionalArgument<string?>("name", "Profile name.");
        var showProviderOption = CreateStringOption("--provider", "Provider override.");
        var showOutputOption = CreateStringOption("--output", "Output mode: json, text, markdown, or dump.");

        authCommand.Add(authNameArgument);
        authCommand.Add(authProviderOption);
        authCommand.Add(authUrlOption);
        authCommand.Add(authOutputOption);
        authCommand.Add(authNoWaitOption);
        authCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var selectedProfile = parseResult.GetValue(authNameArgument);
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, selectedProfile));
            var bootstrapper = host.Services.GetRequiredService<IProfileBootstrapper>();
            var registry = host.Services.GetRequiredService<ISearchProviderRegistry>();
            var browserSessionFactory = host.Services.GetRequiredService<IBrowserSessionFactory>();
            var profile = await bootstrapper.PrepareInteractiveAsync(selectedProfile, parseResult.GetValue(authProviderOption), cancellationToken);
            var provider = registry.GetRequiredProvider(profile.DefaultProvider);

            await using var browserContext = await browserSessionFactory.CreateContextAsync(profile, cancellationToken);
            var page = browserContext.Pages.FirstOrDefault() ?? await browserContext.NewPageAsync();
            var targetUrl = parseResult.GetValue(authUrlOption) ?? provider.SetupUrl ?? "https://example.com";
            await page.GotoAsync(targetUrl);
            Console.Error.WriteLine($"Interactive browser launched for profile '{profile.Name}' using provider '{provider.Name}'.");
            Console.Error.WriteLine($"Opened: {targetUrl}");
            if (provider.Capabilities.SupportsInteractiveSetup)
            {
                Console.Error.WriteLine("Complete any sign-in or consent flow in the browser.");
            }

            if (ParseNullableBool(parseResult.GetValue(authNoWaitOption), "--no-wait") != true)
            {
                Console.Error.WriteLine("Press Enter when setup is complete.");
                Console.ReadLine();
            }

            WriteOutput(profile, parseResult.GetValue(authOutputOption), "text");
            return 0;
        });

        showCommand.Add(showNameArgument);
        showCommand.Add(showProviderOption);
        showCommand.Add(showOutputOption);
        showCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var selectedProfile = parseResult.GetValue(showNameArgument);
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption, selectedProfile));
            var resolver = host.Services.GetRequiredService<IProfileResolver>();
            var profile = await resolver.ResolveAsync(selectedProfile, parseResult.GetValue(showProviderOption), cancellationToken);
            WriteOutput(profile, parseResult.GetValue(showOutputOption), "text");
            return 0;
        });

        command.Add(initCommand);
        command.Add(authCommand);
        command.Add(showCommand);
        return command;
    }

    private static Command BuildMcpCommand(
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption)
    {
        var command = new Command("mcp", "Run the MCP server over stdio.");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            using var host = BuildHost(CreateRuntimeOptions(parseResult, configOption, defaultProviderOption, defaultProfileOption, profilesRootOption, logLevelOption), configureMcp: true);
            await host.RunAsync(cancellationToken);
            return 0;
        });
        return command;
    }

    private static IHost BuildHost(CliRuntimeOptions options, bool configureMcp = false)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(ResolveLogLevel(options));
        builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddRecallCore();
        builder.Services.AddRecallPlaywright();
        builder.Services.AddSingleton(new RuntimeDefaults
        {
            ConfigPath = options.ConfigPath,
            DefaultProvider = options.DefaultProvider,
            DefaultProfile = options.DefaultProfile,
            ProfilesRoot = options.ProfilesRoot,
            LogLevel = options.LogLevel
        });

        if (configureMcp)
        {
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<RecallMcpTools>();
        }

        return builder.Build();
    }

    private static CliRuntimeOptions CreateRuntimeOptions(
        ParseResult parseResult,
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption,
        Option<string?>? selectedProfileOption = null)
        => new(
            parseResult.GetValue(configOption),
            parseResult.GetValue(defaultProviderOption),
            parseResult.GetValue(defaultProfileOption),
            parseResult.GetValue(profilesRootOption),
            parseResult.GetValue(logLevelOption),
            selectedProfileOption is null ? null : parseResult.GetValue(selectedProfileOption));

    private static CliRuntimeOptions CreateRuntimeOptions(
        ParseResult parseResult,
        Option<string?> configOption,
        Option<string?> defaultProviderOption,
        Option<string?> defaultProfileOption,
        Option<string?> profilesRootOption,
        Option<string?> logLevelOption,
        string? selectedProfile)
        => new(
            parseResult.GetValue(configOption),
            parseResult.GetValue(defaultProviderOption),
            parseResult.GetValue(defaultProfileOption),
            parseResult.GetValue(profilesRootOption),
            parseResult.GetValue(logLevelOption),
            selectedProfile);

    private static LogLevel ResolveLogLevel(CliRuntimeOptions options)
    {
        if (TryParseLogLevel(options.LogLevel, out var explicitLevel))
        {
            return explicitLevel;
        }

        var configuredLevel = ReadConfiguredLogLevel(options);
        return TryParseLogLevel(configuredLevel, out var configured) ? configured : LogLevel.Information;
    }

    private static string? ReadConfiguredLogLevel(CliRuntimeOptions options)
    {
        var path = options.ConfigPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = GetCandidateConfigPaths().FirstOrDefault(File.Exists);
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var selectedProfile = options.SelectedProfile ?? options.DefaultProfile;
        if (!string.IsNullOrWhiteSpace(selectedProfile)
            && root.TryGetProperty("profiles", out var profiles)
            && profiles.ValueKind == JsonValueKind.Object)
        {
            foreach (var profile in profiles.EnumerateObject())
            {
                if (string.Equals(profile.Name, selectedProfile, StringComparison.OrdinalIgnoreCase)
                    && profile.Value.TryGetProperty("logLevel", out var profileLogLevel)
                    && profileLogLevel.ValueKind == JsonValueKind.String)
                {
                    return profileLogLevel.GetString();
                }
            }
        }

        return root.TryGetProperty("logLevel", out var logLevel) && logLevel.ValueKind == JsonValueKind.String
            ? logLevel.GetString()
            : null;
    }

    private static IReadOnlyList<string> GetCandidateConfigPaths()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            return
            [
                Path.Combine(xdgConfigHome, "Zakira.Recall", "profiles.json"),
                Path.Combine(xdgConfigHome, "Zakira.Recall.json")
            ];
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            Path.Combine(appData, "Zakira.Recall", "profiles.json"),
            Path.Combine(appData, "Zakira.Recall.json")
        ];
    }

    private static bool TryParseLogLevel(string? value, out LogLevel level)
        => Enum.TryParse(value, ignoreCase: true, out level);

    private static bool? ParseNullableBool(string? value, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Option '{optionName}' expects true or false.");
    }

    private static void WriteOutput(object value, string? mode, string defaultMode)
    {
        var normalizedMode = NormalizeOutputMode(mode, defaultMode);
        var text = normalizedMode switch
        {
            "dump" => value.DumpText(),
            "json" => JsonSerializer.Serialize(value, JsonSupport.Options),
            "markdown" => FormatMarkdown(value),
            _ => FormatText(value)
        };
        Console.Out.WriteLine(text);
    }

    private static string NormalizeOutputMode(string? mode, string defaultMode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return defaultMode;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "dump" => "dump",
            "dumpify" => "dump",
            "json" => "json",
            "markdown" => "markdown",
            "md" => "markdown",
            "text" => "text",
            _ => defaultMode
        };
    }

    private static string FormatText(object value)
        => value switch
        {
            SearchResponse response when response.Results.Count > 0 => string.Join(Environment.NewLine + Environment.NewLine, response.Results.Select(result => $"[{result.Rank}] {result.Title}{Environment.NewLine}{result.Url}{Environment.NewLine}{result.Snippet}".TrimEnd())),
            SearchResponse response => response.Error?.Message ?? "No search results.",
            FetchResponse response when response.Success => $"{response.Title ?? response.FinalUrl}{Environment.NewLine}{response.FinalUrl}{Environment.NewLine}{response.Excerpt}".TrimEnd(),
            FetchResponse response => $"Fetch failed: {response.Error?.Message}",
            ResearchResponse response when response.Citations.Count > 0 => FormatResearchText(response),
            ResearchResponse response => response.Errors.FirstOrDefault()?.Message ?? "No research sources.",
            RecallConfig config => $"Default profile: {config.DefaultProfile ?? "default"}{Environment.NewLine}Default provider: {config.DefaultProvider ?? "duckduckgo"}{Environment.NewLine}Profiles: {config.Profiles.Count}",
            ProfileDescriptor profile => $"Profile: {profile.Name}{Environment.NewLine}Provider: {profile.DefaultProvider}{Environment.NewLine}Channel: {profile.Channel}{Environment.NewLine}Headless: {profile.Headless}{Environment.NewLine}User data: {profile.UserDataDir}",
            SearchProviderDescriptor provider => FormatProviderText(provider),
            SearchProviderDescriptor[] providers => string.Join(Environment.NewLine, providers.Select(provider => $"{provider.Name} | browser={provider.Capabilities.RequiresBrowser} | pagination={provider.Capabilities.SupportsPagination} | timeRange={provider.Capabilities.SupportsTimeRange} | safeSearch={provider.Capabilities.SupportsSafeSearch} | healthy={provider.Health?.IsHealthy}")),
            ProviderHealthSnapshot snapshot => $"Provider: {snapshot.Provider}{Environment.NewLine}Healthy: {snapshot.IsHealthy}{Environment.NewLine}Failures: {snapshot.ConsecutiveFailures}",
            _ => JsonSerializer.Serialize(value, JsonSupport.Options)
        };

    private static string FormatMarkdown(object value)
        => value switch
        {
            SearchResponse response => string.Join(Environment.NewLine, response.Results.Select(result => $"- [{result.Title}]({result.Url}){(string.IsNullOrWhiteSpace(result.Snippet) ? string.Empty : $" - {result.Snippet}")}")),
            FetchResponse response when response.Success => $"# {response.Title ?? response.FinalUrl}{Environment.NewLine}{Environment.NewLine}{response.Text}",
            FetchResponse response => $"# Fetch Failed{Environment.NewLine}{Environment.NewLine}{response.Error?.Message}",
            ResearchResponse response => FormatResearchMarkdown(response),
            RecallConfig config => $"# Config{Environment.NewLine}{Environment.NewLine}- Default profile: `{config.DefaultProfile ?? "default"}`{Environment.NewLine}- Default provider: `{config.DefaultProvider ?? "duckduckgo"}`{Environment.NewLine}- Profiles: {config.Profiles.Count}",
            ProfileDescriptor profile => $"# Profile `{profile.Name}`{Environment.NewLine}{Environment.NewLine}- Provider: `{profile.DefaultProvider}`{Environment.NewLine}- Channel: `{profile.Channel}`{Environment.NewLine}- Headless: `{profile.Headless}`{Environment.NewLine}- User data: `{profile.UserDataDir}`",
            SearchProviderDescriptor provider => FormatProviderMarkdown(provider),
            SearchProviderDescriptor[] providers => string.Join(Environment.NewLine, providers.Select(provider => $"- `{provider.Name}`: browser={provider.Capabilities.RequiresBrowser}, pagination={provider.Capabilities.SupportsPagination}, timeRange={provider.Capabilities.SupportsTimeRange}, safeSearch={provider.Capabilities.SupportsSafeSearch}, healthy={provider.Health?.IsHealthy}")),
            ProviderHealthSnapshot snapshot => $"# Provider Health `{snapshot.Provider}`{Environment.NewLine}{Environment.NewLine}- Healthy: `{snapshot.IsHealthy}`{Environment.NewLine}- Consecutive failures: `{snapshot.ConsecutiveFailures}`",
            _ => FormatText(value)
        };

    private static string FormatProviderText(SearchProviderDescriptor provider)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Provider: {provider.Name}");
        builder.AppendLine($"Aliases: {(provider.Aliases.Count == 0 ? "(none)" : string.Join(", ", provider.Aliases))}");
        builder.AppendLine($"Setup URL: {provider.SetupUrl ?? "(none)"}");
        builder.AppendLine($"Browser: {provider.Capabilities.RequiresBrowser}");
        builder.AppendLine($"Pagination: {provider.Capabilities.SupportsPagination}");
        builder.AppendLine($"Time range: {provider.Capabilities.SupportsTimeRange}");
        builder.AppendLine($"Safe search: {provider.Capabilities.SupportsSafeSearch}");
        builder.AppendLine($"Interactive setup: {provider.Capabilities.SupportsInteractiveSetup}");
        if (provider.Health is not null)
        {
            builder.AppendLine($"Healthy: {provider.Health.IsHealthy}");
            builder.Append($"Consecutive failures: {provider.Health.ConsecutiveFailures}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatResearchText(ResearchResponse response)
    {
        var sections = new List<string>();
        if (!string.IsNullOrWhiteSpace(response.Summary))
        {
            sections.Add($"Summary{Environment.NewLine}{response.Summary}");
        }

        sections.AddRange(response.Citations.Select(citation => $"[{citation.Id}] {citation.Title}{Environment.NewLine}{citation.Url}{Environment.NewLine}{citation.Quote}".TrimEnd()));
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string FormatResearchMarkdown(ResearchResponse response)
    {
        var sections = new List<string>();
        if (!string.IsNullOrWhiteSpace(response.Summary))
        {
            sections.Add($"# Summary{Environment.NewLine}{Environment.NewLine}{response.Summary}");
        }

        sections.AddRange(response.Citations.Select(citation => $"## {citation.Id}: [{citation.Title}]({citation.Url}){Environment.NewLine}{Environment.NewLine}{citation.Quote}".TrimEnd()));
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string FormatProviderMarkdown(SearchProviderDescriptor provider)
        => $"# Provider `{provider.Name}`{Environment.NewLine}{Environment.NewLine}" +
           $"- Aliases: {(provider.Aliases.Count == 0 ? "(none)" : string.Join(", ", provider.Aliases.Select(alias => $"`{alias}`")))}{Environment.NewLine}" +
           $"- Setup URL: {(provider.SetupUrl is null ? "(none)" : $"`{provider.SetupUrl}`")}{Environment.NewLine}" +
           $"- Browser: `{provider.Capabilities.RequiresBrowser}`{Environment.NewLine}" +
           $"- Pagination: `{provider.Capabilities.SupportsPagination}`{Environment.NewLine}" +
           $"- Time range: `{provider.Capabilities.SupportsTimeRange}`{Environment.NewLine}" +
           $"- Safe search: `{provider.Capabilities.SupportsSafeSearch}`{Environment.NewLine}" +
           $"- Interactive setup: `{provider.Capabilities.SupportsInteractiveSetup}`{Environment.NewLine}" +
           (provider.Health is null
               ? string.Empty
               : $"- Healthy: `{provider.Health.IsHealthy}`{Environment.NewLine}- Consecutive failures: `{provider.Health.ConsecutiveFailures}`");

    private static Option<string?> CreateStringOption(string name, string description, bool recursive = false)
        => new(name)
        {
            Description = description,
            Recursive = recursive
        };

    private static Option<int?> CreateIntOption(string name, string description)
        => new(name)
        {
            Description = description
        };

    private static Option<string[]> CreateStringListOption(string name, string description)
        => new(name)
        {
            Description = description,
            AllowMultipleArgumentsPerToken = true
        };

    private static Argument<T> CreateRequiredArgument<T>(string name, string description)
        => new(name)
        {
            Description = description,
            Arity = ArgumentArity.ExactlyOne
        };

    private static Argument<T> CreateOptionalArgument<T>(string name, string description)
        => new(name)
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        };
}

internal static class JsonSupport
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}

internal sealed record CliRuntimeOptions(
    string? ConfigPath,
    string? DefaultProvider,
    string? DefaultProfile,
    string? ProfilesRoot,
    string? LogLevel,
    string? SelectedProfile);

[McpServerToolType]
internal sealed class RecallMcpTools(
    ISearchService searchService,
    IFetchService fetchService,
    IResearchService researchService,
    ISearchProviderRegistry providerRegistry,
    IProviderHealthTracker healthTracker,
    IProfileResolver profileResolver,
    IRecallConfigLoader configLoader)
{
    [McpServerTool, Description("Search the web using the selected provider or the configured default.")]
    public async Task<SearchResponse> WebSearch(
        [Description("The raw search query, including operators such as site: or filetype:.")] string query,
        [Description("Optional provider override, such as duckduckgo, duckduckgo-browser, or bing.")] string? provider = null,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Maximum number of results to return.")] int maxResults = 10,
        [Description("Result page number.")] int page = 1,
        [Description("Optional time range such as day, week, month, or year.")] string? timeRange = null,
        [Description("Optional safe search override.")] bool? safeSearch = null,
        [Description("Enable or disable provider fallback.")] bool? enableFallback = null,
        [Description("Optional fallback providers.")] string[]? fallbackProviders = null)
    {
        return await searchService.SearchAsync(new SearchRequest
        {
            Query = query,
            Provider = provider,
            Profile = profile,
            MaxResults = maxResults,
            Page = page,
            TimeRange = timeRange,
            SafeSearch = safeSearch,
            EnableFallback = enableFallback,
            FallbackProviders = fallbackProviders ?? []
        });
    }

    [McpServerTool, Description("Fetch readable content from a single URL.")]
    public async Task<FetchResponse> WebFetch(
        [Description("The URL to open and extract readable text from.")] string url,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Navigation timeout in seconds.")] int timeoutSeconds = 30)
    {
        return await fetchService.FetchAsync(new FetchRequest
        {
            Url = url,
            Profile = profile,
            TimeoutSeconds = timeoutSeconds
        });
    }

    [McpServerTool, Description("Search the web, fetch top pages, and return a research bundle with structured citations.")]
    public async Task<ResearchResponse> WebResearch(
        [Description("The raw research query.")] string query,
        [Description("Optional provider override, such as duckduckgo, duckduckgo-browser, or bing.")] string? provider = null,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Maximum number of search results to collect.")] int maxResults = 8,
        [Description("Number of top result pages to fetch.")] int topPagesToRead = 3,
        [Description("Result page number.")] int page = 1,
        [Description("Optional time range such as day, week, month, or year.")] string? timeRange = null,
        [Description("Optional safe search override.")] bool? safeSearch = null,
        [Description("Enable or disable provider fallback.")] bool? enableFallback = null,
        [Description("Optional fallback providers.")] string[]? fallbackProviders = null,
        [Description("Maximum number of concurrent fetches.")] int? maxConcurrentFetches = null,
        [Description("Prefer unique domains when choosing result pages to fetch.")] bool enforceDomainDiversity = true)
    {
        return await researchService.ResearchAsync(new ResearchRequest
        {
            Query = query,
            Provider = provider,
            Profile = profile,
            MaxResults = maxResults,
            TopPagesToRead = topPagesToRead,
            Page = page,
            TimeRange = timeRange,
            SafeSearch = safeSearch,
            EnableFallback = enableFallback,
            FallbackProviders = fallbackProviders ?? [],
            MaxConcurrentFetches = maxConcurrentFetches,
            EnforceDomainDiversity = enforceDomainDiversity
        });
    }

    [McpServerTool, Description("List providers with capabilities and current health state.")]
    public async Task<SearchProviderDescriptor[]> WebListProviders(
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null)
    {
        var resolvedProfile = await profileResolver.ResolveAsync(profile, providerOverride: null);
        return providerRegistry.GetProviders()
            .Select(provider => new SearchProviderDescriptor
            {
                Name = provider.Name,
                Aliases = provider.Aliases,
                SetupUrl = provider.SetupUrl,
                Capabilities = provider.Capabilities,
                Health = healthTracker.GetSnapshot(provider.Name, resolvedProfile.ProviderHealthCooldownSeconds)
            })
            .ToArray();
    }

    [McpServerTool, Description("Fetch readable content from multiple URLs in one call.")]
    public async Task<FetchResponse[]> WebBatchFetch(
        [Description("The URLs to fetch.")] string[] urls,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Navigation timeout in seconds.")] int timeoutSeconds = 30)
    {
        var tasks = urls.Select(url => fetchService.FetchAsync(new FetchRequest
        {
            Url = url,
            Profile = profile,
            TimeoutSeconds = timeoutSeconds
        }).AsTask());
        return await Task.WhenAll(tasks);
    }

    [McpServerTool, Description("Search the web, then fetch a selected subset of result URLs.")]
    public async Task<FetchResponse[]> WebSearchThenFetch(
        [Description("The raw search query.")] string query,
        [Description("Zero-based result indexes to fetch from the search response.")] int[] selectedResultIndexes,
        [Description("Optional provider override.")] string? provider = null,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Maximum number of search results to collect.")] int maxResults = 8,
        [Description("Result page number.")] int page = 1,
        [Description("Navigation timeout in seconds.")] int timeoutSeconds = 30)
    {
        var search = await searchService.SearchAsync(new SearchRequest
        {
            Query = query,
            Provider = provider,
            Profile = profile,
            MaxResults = maxResults,
            Page = page
        });

        var urls = selectedResultIndexes
            .Where(index => index >= 0 && index < search.Results.Count)
            .Select(index => search.Results[index].Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await WebBatchFetch(urls, profile, timeoutSeconds);
    }

    [McpServerTool, Description("Show the resolved configuration used by Zakira.Recall.")]
    public async Task<RecallConfig> WebShowConfig()
        => await configLoader.LoadAsync();

    [McpServerTool, Description("Show the resolved profile after applying config defaults and provider overrides.")]
    public async Task<ProfileDescriptor> WebShowProfile(
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Optional provider override.")] string? provider = null)
        => await profileResolver.ResolveAsync(profile, provider);

    [McpServerTool, Description("Show current health state for a provider.")]
    public async Task<ProviderHealthSnapshot> WebGetProviderHealth(
        [Description("Provider name.")] string provider,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null)
    {
        var resolvedProfile = await profileResolver.ResolveAsync(profile, providerOverride: null);
        var normalizedProvider = providerRegistry.NormalizeProviderName(provider) ?? provider;
        return healthTracker.GetSnapshot(normalizedProvider, resolvedProfile.ProviderHealthCooldownSeconds);
    }
}
