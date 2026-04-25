# Zakira.Recall

[![Test](https://github.com/MoaidHathot/Zakira.Recall/actions/workflows/test.yml/badge.svg)](https://github.com/MoaidHathot/Zakira.Recall/actions/workflows/test.yml)
[![NuGet](https://img.shields.io/nuget/v/Zakira.Recall)](https://www.nuget.org/packages/Zakira.Recall)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Local CLI and MCP server for web search, page fetch, and research workflows without API keys.

Supports:

- provider selection and fallback
- Playwright-backed search and fetch
- structured citations for research results
- JSON, text, Markdown, and Dumpify CLI output modes
- interactive browser profile setup for sign-in and consent flows
- config and profile inspection commands

## Install

From NuGet as a global tool:

```powershell
dotnet tool install --global Zakira.Recall
```

Available commands after install:

```powershell
recall --help
zakira.recall --help
```

## Commands

```powershell
recall search "site:github.com mcp server"
recall search "playwright mcp" --provider duckduckgo-browser --page 2 --time-range month --safe-search false
recall fetch "https://example.com"
recall fetch "https://example.com" --output markdown
recall research "best local mcp web search tools" --domain-diversity true
recall research "playwright search providers" --fallback-provider bing --max-concurrent-fetches 4 --output dump
recall config init
recall config show --output dump
recall providers list --output json
recall profile show default --output markdown
recall profile init default --channel msedge --provider duckduckgo --headless false
recall profile auth interactive --provider duckduckgo-browser
recall mcp
```

## Providers

Available providers:

- `duckduckgo`
- `duckduckgo-browser`
- `bing`

List provider capabilities and current health state:

```powershell
recall providers list
recall providers list --output json
recall providers list --output dump
```

The browser-backed providers use Playwright and support interactive setup flows.

## Playwright Setup

Search providers that use Playwright and the `fetch` flow require the Playwright runtime and browser install.

If you are running from build output:

```powershell
pwsh "$env:LOCALAPPDATA\Temp\Zakira.Recall\bin\Debug\net10.0\playwright.ps1" install chromium
```

If you installed the global tool, run the packaged `playwright.ps1` next to the installed tool files.

If you want to prepare a persistent interactive browser profile for consent pages or sign-in:

```powershell
recall profile auth interactive --provider duckduckgo-browser
recall profile auth interactive --provider bing
```

This opens a non-headless browser using the selected profile and waits for you to press Enter after setup is complete.

## Config

Default config file locations:

- If `XDG_CONFIG_HOME` is set:
  `$(XDG_CONFIG_HOME)/Zakira.Recall/profiles.json`
- Otherwise on Windows:
  `%APPDATA%\Zakira.Recall\profiles.json`

Default profile storage locations:

- If `XDG_DATA_HOME` is set:
  `$(XDG_DATA_HOME)/Zakira.Recall/profiles`
- Otherwise on Windows:
  `%LOCALAPPDATA%\Zakira.Recall\profiles`

Example config file for `$XDG_CONFIG_HOME/Zakira.Recall/profiles.json`:

- `examples/profiles.json`

Generate it with the CLI:

```powershell
recall config init
```

By default this writes to:

- `$XDG_CONFIG_HOME/Zakira.Recall/profiles.json` when `XDG_CONFIG_HOME` is set
- otherwise `%APPDATA%\Zakira.Recall\profiles.json`

Override the output path if needed:

```powershell
recall config init --path "C:/temp/recall-profiles.json"
```

Inspect the resolved configuration:

```powershell
recall config show
recall config show --path "C:/temp/recall-profiles.json" --output dump
```

Generate a config with fallback providers and logging defaults:

```powershell
recall config init --provider duckduckgo --fallback-provider duckduckgo-browser bing --enable-fallback true --config-log-level Information
```

Useful config fields:

- `defaultProvider`
- `defaultProfile`
- `profilesRoot`
- `fallbackProviders`
- `enableProviderFallback`
- `providerHealthCooldownSeconds`
- `maxConcurrentFetches`
- `logLevel`

Inspect a resolved profile:

```powershell
recall profile show
recall profile show default --provider bing --output markdown
```

Per-profile fields:

- `name`
- `channel`
- `defaultProvider`
- `headless`
- `userDataDir`
- `locale`
- `timeoutSeconds`
- `fallbackProviders`
- `enableProviderFallback`
- `providerHealthCooldownSeconds`
- `maxConcurrentFetches`
- `logLevel`
- `metadata`

Notes:

- If `profilesRoot` is omitted, the tool falls back to the default data directory.
- If `defaultProfile` is omitted, the tool uses `default`.
- If a profile omits `channel`, the tool uses `msedge`.
- If a profile omits `headless`, the resolver defaults it to `true`.
- If a profile omits `timeoutSeconds`, the resolver defaults it to `30`.
- If `enableProviderFallback` is enabled, search can fail over to configured fallback providers.
- Provider health is tracked in-memory during the current process and used to avoid retrying recently failing providers.
- `logLevel` can be set globally in config, per profile, or overridden at runtime with `--log-level`.

## CLI Options

Common search options:

```powershell
recall search <query> \
  [--provider <name>] \
  [--profile <name>] \
  [--limit <n>] \
  [--page <n>] \
  [--time-range <day|week|month|year>] \
  [--safe-search <true|false>] \
  [--fallback <true|false>] \
  [--fallback-provider <name> ...] \
  [--output <json|text|markdown|dump>]
```

Fetch options:

```powershell
recall fetch <url> \
  [--profile <name>] \
  [--timeout <seconds>] \
  [--output <json|text|markdown|dump>]
```

Research options:

```powershell
recall research <query> \
  [--provider <name>] \
  [--profile <name>] \
  [--limit <n>] \
  [--top-pages <n>] \
  [--page <n>] \
  [--time-range <day|week|month|year>] \
  [--safe-search <true|false>] \
  [--fallback <true|false>] \
  [--fallback-provider <name> ...] \
  [--max-concurrent-fetches <n>] \
  [--domain-diversity <true|false>] \
  [--output <json|text|markdown|dump>]
```

Profile inspection options:

```powershell
recall profile show [name] \
  [--provider <name>] \
  [--output <json|text|markdown|dump>]
```

Config inspection options:

```powershell
recall config show \
  [--path <path>] \
  [--output <json|text|markdown|dump>]
```

Global options:

```powershell
--config <path>
--default-provider <name>
--default-profile <name>
--profiles-root <path>
--log-level <Trace|Debug|Information|Warning|Error|Critical|None>
```

## Output

CLI commands support multiple output modes:

- `json`: full structured response
- `text`: concise terminal-friendly output
- `markdown`: readable output for notes or LLM workflows
- `dump`: structured object inspection via Dumpify

Examples:

```powershell
recall search "mcp server" --output text
recall fetch "https://example.com" --output markdown
recall research "playwright search" --output json
recall providers list --output dump
```

## Research Output

`research` returns:

- normalized search results
- fetched page content for selected sources
- structured citations
- partial errors when some pages fail but others succeed

By default, `research` also:

- deduplicates equivalent result URLs before fetch
- prefers domain diversity when selecting top pages to read

This makes the tool safer for agent workflows because one failed fetch does not have to fail the whole research run.

## MCP

Run the MCP server over stdio:

```powershell
recall mcp
```

The exposed tools are:

- `WebSearch`
- `WebFetch`
- `WebResearch`
- `WebListProviders`
- `WebBatchFetch`
- `WebSearchThenFetch`
- `WebShowConfig`
- `WebShowProfile`
- `WebGetProviderHealth`

MCP tools return structured typed results that are easier for agents to consume directly.

`WebSearch` and `WebResearch` support provider selection, paging, time range, safe search, and fallback controls.

## Pack

Pack the NuGet tool locally:

```powershell
pwsh .\pack.ps1
```

Pack and push to NuGet.org:

```powershell
pwsh .\pack.ps1 -Push
pwsh .\pack.ps1 -ApiKey "<key>" -Push
```

If `-ApiKey` is omitted, `pack.ps1` uses `NUGET_API_KEY`.
