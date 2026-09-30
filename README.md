# Zakira.Recall

[![Test](https://github.com/MoaidHathot/Zakira.Recall/actions/workflows/test.yml/badge.svg)](https://github.com/MoaidHathot/Zakira.Recall/actions/workflows/test.yml)
[![NuGet](https://img.shields.io/nuget/v/Zakira.Recall)](https://www.nuget.org/packages/Zakira.Recall)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Local CLI and MCP server for web search, page fetch, and research workflows without API keys.

Supports:

- provider selection and fallback
- pluggable provider discovery with alias support
- Playwright-backed search and fetch
- structured citations for research results
- repeatable quality evaluation reports for search/research results
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
```

## Commands

```powershell
recall search "site:github.com mcp server"
recall search "playwright mcp" --provider duckduckgo-browser --page 2 --time-range month --safe-search false
recall fetch "https://example.com"
recall fetch "https://example.com" --output markdown
recall research "best local mcp web search tools" --domain-diversity true
recall research "playwright search providers" --fallback-provider bing --max-concurrent-fetches 4 --output dump
recall eval run examples/eval-dataset.json --provider bing --output markdown --report artifacts/eval-report.md
recall config init
recall config show --output dump
recall providers list --output json
recall providers test ddg
recall profile show default --output markdown
recall profile init default --channel msedge --provider duckduckgo --headless false
recall profile auth interactive --provider duckduckgo-browser --no-wait true
recall mcp
```

## Providers

Available providers:

- `duckduckgo`
- `duckduckgo-browser`
- `bing`

Provider names are resolved through the registry, so aliases such as `ddg` work anywhere a provider name is accepted.

List provider capabilities and current health state:

```powershell
recall providers list
recall providers list --output json
recall providers list --output dump
recall providers test ddg
```

The browser-backed providers use Playwright and support interactive setup flows.

For contributors: provider registration is assembly-driven. New `ISearchProvider` implementations in the Playwright assembly are discovered automatically, and aliases plus setup URLs come from the provider itself.

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
recall profile auth interactive --provider bing --no-wait true
```

This opens a non-headless browser using the selected profile, navigates to the provider's setup URL, and prints guidance in the terminal. By default it waits for Enter before closing. Use `--no-wait true` if you want the command to return immediately after opening the page.

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

User-facing commands default to `text` output. Use `--output json` when you want machine-oriented structured output.

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

`--output markdown` prints the readable text and, when the page declares one, a "Structured data" section with the
schema.org main entity as JSON (see [Fetch Output](#fetch-output)).

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

Evaluation options:

```powershell
recall eval run <dataset.json> \
  [--provider <name>] \
  [--profile <name>] \
  [--limit <n>] \
  [--top-pages <n>] \
  [--fallback <true|false>] \
  [--fallback-provider <name> ...] \
  [--report <path>] \
  [--fail-under <score>] \
  [--output <json|text|markdown|dump>]

recall eval score <dataset.json> <responses.json> \
  [--report <path>] \
  [--fail-under <score>] \
  [--output <json|text|markdown|dump>]
```

`eval run` executes live research for each case in the dataset and scores the result. `eval score` scores previously captured `ResearchResponse` objects without making live web requests.

Dataset cases can define:

- `query`: the research query to run
- `expectedDomains`: domains that should appear in the search results
- `requiredTerms`: terms that should appear in the search, fetch, citation, or summary text
- `badDomains`: domains that should not appear

Start with `examples/eval-dataset.json`, then add real failure cases as they are found.

Profile inspection options:

```powershell
recall profile show [name] \
  [--provider <name>] \
  [--output <json|text|markdown|dump>]
```

Provider inspection options:

```powershell
recall providers list \
  [--profile <name>] \
  [--output <json|text|markdown|dump>]

recall providers test <name> \
  [--profile <name>] \
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
recall search "mcp server"
recall fetch "https://example.com" --output markdown
recall research "playwright search" --output json
recall providers list --output dump
recall providers test ddg
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

## Fetch Output

`fetch` (and the `web_fetch` / `web_batch_fetch` MCP tools) renders the page in a real browser, serializes the
rendered DOM and extracts readable content in-process. A `FetchResponse` contains:

- `title`, `text`, `excerpt` (single line, 400 chars), `wordCount`, `domain`, `siteName`, `publishedAt`
- `statusCode`: HTTP status of the main document
- `contentSelector`: the element the text was taken from (`main.main`, `article#post-7`, `body`, ...), useful when
  checking extraction quality
- `mainImage`: `og:image`, `twitter:image` or the main entity's image, as an absolute URL
- `structuredData`: schema.org JSON-LD declared by the page: the root entity `types`, and the most relevant content
  entity (`Recipe`, `HowTo`, `NewsArticle`/`Article`, `Product`, `Event`, `FAQPage`, ...) as `mainEntity`, returned
  inline with bulky members (reviews, comments, actions) removed. Recipes therefore arrive with `recipeIngredient`,
  `recipeInstructions`, ISO 8601 durations, yield and nutrition without any HTML parsing on the caller's side.
  Site chrome (`WebSite`, `WebPage`, `BreadcrumbList`) is never the main entity.
- `error` with `code` and `transient` when the page is not usable:
  - `fetch_http_error`: the server answered 4xx/5xx (transient for 408/425/429/5xx); the page text is still returned
  - `fetch_bot_challenge`: a challenge or block page (Cloudflare, Akamai, PerimeterX, DuckDuckGo, ...); transient
  - `fetch_login_required`: a login wall
  - `fetch_weak_content`: fewer than 15 readable words
  - `fetch_failed`: the browser could not load the page; `transient` is `true` for timeouts, closed pages and driver restarts

How the text is chosen: every plausible container (`article`, `main`, `[role=main]`, SPA roots, common CMS wrappers) is
scored by its boilerplate-free word count and link density; semantic containers get a small edge; if the best one holds
less than 40% of the page's words (a related-post card, a comment, a header teaser) the whole body is used instead.
Navigation, headers, footers, hidden elements, comment areas, tables of contents, breadcrumbs and share widgets are
removed; an `<aside>` inside the article is kept unless it is almost entirely links. Text keeps its block structure:
paragraphs, headings, `-` / `1.` bullets, `|`-separated table cells and preformatted blocks.

`publishedAt` and `siteName` fall back to JSON-LD (`datePublished`, `publisher`) when meta tags are missing; dates such
as `July 8, 2024 at 2:48pm` are understood.

## MCP

Run the MCP server over stdio:

```powershell
recall mcp
```

The exposed tools are:

- `web_search`
- `web_fetch`
- `web_research`
- `web_list_providers`
- `web_batch_fetch` (concurrency bounded by the profile's `maxConcurrentFetches`, overridable per call)
- `web_search_then_fetch`
- `web_show_config`
- `web_show_profile`
- `web_get_provider_health`

MCP tools return structured typed results that are easier for agents to consume directly.

`web_search` and `web_research` support provider selection, paging, time range, safe search, and fallback controls.

### Browser lifecycle

Fetches share one Playwright driver process per host. If that process dies (a crash, or an external "kill every
`node.exe`" sweep), the next fetch starts a new driver and retries once instead of failing until the host restarts.
Headless fetches run in a throw-away copy of the profile that excludes caches (about 2 MB instead of 30+ MB); the
copy is deleted after the fetch, and copies older than an hour are swept at start-up.

A headless Chromium/Edge reports `HeadlessChrome/<version>` as its product token although it is the same browser
build, and a number of sites answer that token with a reduced page or a "Just a moment..." verification page instead
of the article. The fetcher reads the browser's own user agent once per channel and presents it without the headless
marker (same version, so it stays consistent with the `sec-ch-ua` client hints the browser sends).

## Agent Skills

Zakira.Recall ships an [Agent Skill](https://agentskills.io) so MCP-compatible coding agents (Claude Code, OpenCode, Cursor, GitHub Copilot, Gemini CLI, and others) can discover when and how to use it without you having to spell it out in every prompt.

The skill lives in this repo at:

- `skills/zakira-recall/`

It is a single skill called `zakira-recall` with a small `SKILL.md` index that routes the agent to focused references for each task:

- `references/search.md` — `WebSearch` / `recall search`
- `references/research.md` — `WebResearch` / `recall research`
- `references/fetch.md` — `WebFetch`, `WebBatchFetch`, `WebSearchThenFetch` / `recall fetch`
- `references/setup.md` — install, Playwright, MCP registration
- `references/troubleshooting.md` — consent pages, captchas, provider health, fallback
- `references/mcp-tools.md` — complete MCP tool reference

### Install the skill

Most agents look for skills in well-known directories. Copy or symlink the `skills/zakira-recall/` folder into the location your agent uses:

```powershell
# OpenCode (global)
New-Item -ItemType Directory -Path "$env:USERPROFILE\.config\opencode\skills" -Force | Out-Null
Copy-Item -Recurse -Force "skills\zakira-recall" "$env:USERPROFILE\.config\opencode\skills\zakira-recall"

# Claude Code (global)
New-Item -ItemType Directory -Path "$env:USERPROFILE\.claude\skills" -Force | Out-Null
Copy-Item -Recurse -Force "skills\zakira-recall" "$env:USERPROFILE\.claude\skills\zakira-recall"

# Generic .agents convention (global)
New-Item -ItemType Directory -Path "$env:USERPROFILE\.agents\skills" -Force | Out-Null
Copy-Item -Recurse -Force "skills\zakira-recall" "$env:USERPROFILE\.agents\skills\zakira-recall"
```

On Linux or macOS:

```bash
mkdir -p ~/.config/opencode/skills && cp -r skills/zakira-recall ~/.config/opencode/skills/
mkdir -p ~/.claude/skills        && cp -r skills/zakira-recall ~/.claude/skills/
mkdir -p ~/.agents/skills        && cp -r skills/zakira-recall ~/.agents/skills/
```

You can also install it per-project by copying the folder into `.opencode/skills/`, `.claude/skills/`, or `.agents/skills/` inside the consuming project. See your agent's docs for the exact lookup paths. For the full list of skills-compatible products, see [agentskills.io/home](https://agentskills.io/home).

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
