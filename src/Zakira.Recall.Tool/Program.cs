using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.DependencyInjection;
using Zakira.Recall.Playwright.DependencyInjection;

return await ZakiraRecallProgram.RunAsync(args);

internal static class ZakiraRecallProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        var parsed = CliArguments.Parse(args);
        if (parsed.ShowHelp || string.IsNullOrWhiteSpace(parsed.Command))
        {
            WriteUsage();
            return 0;
        }

        return parsed.Command switch
        {
            "search" => await RunSearchAsync(parsed),
            "fetch" => await RunFetchAsync(parsed),
            "research" => await RunResearchAsync(parsed),
            "profile" => await RunProfileAsync(parsed),
            "mcp" => await RunMcpAsync(parsed),
            _ => ThrowUnknownCommand(parsed.Command)
        };
    }

    private static async Task<int> RunSearchAsync(CliArguments parsed)
    {
        var query = parsed.RequirePositional(0, "search query");
        using var host = BuildHost(parsed);
        var service = host.Services.GetRequiredService<ISearchService>();
        var response = await service.SearchAsync(new SearchRequest
        {
            Query = query,
            Provider = parsed.GetOption("provider"),
            Profile = parsed.GetOption("profile"),
            MaxResults = parsed.GetIntOption("limit", 10)
        });

        Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonSupport.Options));
        return 0;
    }

    private static async Task<int> RunFetchAsync(CliArguments parsed)
    {
        var url = parsed.RequirePositional(0, "url");
        using var host = BuildHost(parsed);
        var service = host.Services.GetRequiredService<IFetchService>();
        var response = await service.FetchAsync(new FetchRequest
        {
            Url = url,
            Profile = parsed.GetOption("profile")
        });

        Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonSupport.Options));
        return 0;
    }

    private static async Task<int> RunResearchAsync(CliArguments parsed)
    {
        var query = parsed.RequirePositional(0, "research query");
        using var host = BuildHost(parsed);
        var service = host.Services.GetRequiredService<IResearchService>();
        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = query,
            Provider = parsed.GetOption("provider"),
            Profile = parsed.GetOption("profile"),
            MaxResults = parsed.GetIntOption("limit", 8),
            TopPagesToRead = parsed.GetIntOption("top-pages", 3)
        });

        Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonSupport.Options));
        return 0;
    }

    private static async Task<int> RunProfileAsync(CliArguments parsed)
    {
        if (!string.Equals(parsed.Subcommand, "init", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Supported profile subcommands: init");
        }

        var name = parsed.RequirePositional(0, "profile name");
        using var host = BuildHost(parsed);
        var bootstrapper = host.Services.GetRequiredService<IProfileBootstrapper>();
        var profile = await bootstrapper.EnsureProfileAsync(
            name,
            parsed.GetOption("channel") ?? "msedge",
            parsed.GetOption("provider") ?? "duckduckgo",
            parsed.GetBoolOption("headless"),
            parsed.GetOption("user-data-dir"));

        Directory.CreateDirectory(profile.UserDataDir);
        Console.Out.WriteLine(JsonSerializer.Serialize(profile, JsonSupport.Options));
        return 0;
    }

    private static async Task<int> RunMcpAsync(CliArguments parsed)
    {
        using var host = BuildHost(parsed, configureMcp: true);
        await host.RunAsync();
        return 0;
    }

    private static IHost BuildHost(CliArguments parsed, bool configureMcp = false)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddRecallCore();
        builder.Services.AddRecallPlaywright();
        builder.Services.AddSingleton(new RuntimeDefaults
        {
            ConfigPath = parsed.GetOption("config"),
            DefaultProvider = parsed.GetOption("default-provider"),
            DefaultProfile = parsed.GetOption("default-profile"),
            ProfilesRoot = parsed.GetOption("profiles-root")
        });

        if (configureMcp)
        {
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<RecallMcpTools>();
        }

        return builder.Build();
    }

    private static int ThrowUnknownCommand(string command)
        => throw new InvalidOperationException($"Unknown command '{command}'. Use --help to see supported commands.");

    private static void WriteUsage()
    {
        Console.Out.WriteLine("""
        Zakira.Recall

        Commands:
          search <query> [--provider <name>] [--profile <name>] [--limit <n>]
          fetch <url> [--profile <name>]
          research <query> [--provider <name>] [--profile <name>] [--limit <n>] [--top-pages <n>]
          profile init <name> [--channel <name>] [--provider <name>] [--headless <true|false>] [--user-data-dir <path>]
          mcp

        Global options:
          --config <path>
          --default-provider <name>
          --default-profile <name>
          --profiles-root <path>
        """);
    }
}

internal static class JsonSupport
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}

internal sealed class CliArguments
{
    private CliArguments() { }

    public string? Command { get; private set; }

    public string? Subcommand { get; private set; }

    public bool ShowHelp { get; private set; }

    public List<string> Positionals { get; } = [];

    public Dictionary<string, string?> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static CliArguments Parse(string[] args)
    {
        var result = new CliArguments();
        var index = 0;

        while (index < args.Length)
        {
            var token = args[index];
            if (token is "--help" or "-h")
            {
                result.ShowHelp = true;
                index++;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                break;
            }

            index = ReadOption(args, index, result.Options);
        }

        if (index >= args.Length)
        {
            return result;
        }

        result.Command = args[index++];
        if (string.Equals(result.Command, "profile", StringComparison.OrdinalIgnoreCase) && index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
        {
            result.Subcommand = args[index++];
        }

        while (index < args.Length)
        {
            var token = args[index];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                index = ReadOption(args, index, result.Options);
            }
            else
            {
                result.Positionals.Add(token);
                index++;
            }
        }

        return result;
    }

    public string RequirePositional(int index, string name)
        => index < Positionals.Count
            ? Positionals[index]
            : throw new InvalidOperationException($"Missing required {name}.");

    public string? GetOption(string name)
        => Options.TryGetValue(name, out var value) ? value : null;

    public int GetIntOption(string name, int fallback)
        => Options.TryGetValue(name, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    public bool? GetBoolOption(string name)
    {
        if (!Options.TryGetValue(name, out var value))
        {
            return null;
        }

        if (value is null)
        {
            return true;
        }

        return bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Option '--{name}' expects true or false.");
    }

    private static int ReadOption(string[] args, int index, IDictionary<string, string?> options)
    {
        var raw = args[index];
        var name = raw[2..];
        string? value = null;

        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[index + 1];
            index += 2;
        }
        else
        {
            index++;
        }

        options[name] = value;
        return index;
    }
}

[McpServerToolType]
internal sealed class RecallMcpTools(ISearchService searchService, IFetchService fetchService, IResearchService researchService)
{
    [McpServerTool, Description("Search the web using the selected provider or the configured default.")]
    public async Task<string> WebSearch(
        [Description("The raw search query, including operators such as site: or filetype:.")] string query,
        [Description("Optional provider override, such as duckduckgo or bing.")] string? provider = null,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Maximum number of results to return.")] int maxResults = 10)
    {
        var response = await searchService.SearchAsync(new SearchRequest
        {
            Query = query,
            Provider = provider,
            Profile = profile,
            MaxResults = maxResults
        });

        return JsonSerializer.Serialize(response, JsonSupport.Options);
    }

    [McpServerTool, Description("Fetch readable content from a single URL.")]
    public async Task<string> WebFetch(
        [Description("The URL to open and extract readable text from.")] string url,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null)
    {
        var response = await fetchService.FetchAsync(new FetchRequest
        {
            Url = url,
            Profile = profile
        });

        return JsonSerializer.Serialize(response, JsonSupport.Options);
    }

    [McpServerTool, Description("Search the web, fetch top pages, and return a deterministic research bundle with sources.")]
    public async Task<string> WebResearch(
        [Description("The raw research query.")] string query,
        [Description("Optional provider override, such as duckduckgo or bing.")] string? provider = null,
        [Description("Optional named profile managed by Zakira.Recall.")] string? profile = null,
        [Description("Maximum number of search results to collect.")] int maxResults = 8,
        [Description("Number of top result pages to fetch.")] int topPagesToRead = 3)
    {
        var response = await researchService.ResearchAsync(new ResearchRequest
        {
            Query = query,
            Provider = provider,
            Profile = profile,
            MaxResults = maxResults,
            TopPagesToRead = topPagesToRead
        });

        return JsonSerializer.Serialize(response, JsonSupport.Options);
    }
}
