# Zakira.Recall

[![Test](https://github.com/MoaidHathot/Zakira.Recall/actions/workflows/test.yml/badge.svg)](https://github.com/MoaidHathot/Zakira.Recall/actions/workflows/test.yml)
[![NuGet](https://img.shields.io/nuget/v/Zakira.Recall)](https://www.nuget.org/packages/Zakira.Recall)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Local CLI and MCP server for web search, page fetch, and research workflows without API keys.

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
recall fetch "https://example.com"
recall research "best local mcp web search tools"
recall config init
recall profile init default --channel msedge --provider duckduckgo --headless false
recall mcp
```

## Playwright Setup

Search providers that use Playwright and the `fetch` flow require the Playwright runtime and browser install.

If you are running from build output:

```powershell
pwsh "$env:LOCALAPPDATA\Temp\Zakira.Recall\bin\Debug\net10.0\playwright.ps1" install chromium
```

If you installed the global tool, run the packaged `playwright.ps1` next to the installed tool files.

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

Notes:

- If `profilesRoot` is omitted, the tool falls back to the default data directory.
- If `defaultProfile` is omitted, the tool uses `default`.
- If a profile omits `channel`, the tool uses `msedge`.
- If a profile omits `headless`, the resolver defaults it to `true`.
- If a profile omits `timeoutSeconds`, the resolver defaults it to `30`.

## MCP

Run the MCP server over stdio:

```powershell
recall mcp
```

The exposed tools are:

- `WebSearch`
- `WebFetch`
- `WebResearch`

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
