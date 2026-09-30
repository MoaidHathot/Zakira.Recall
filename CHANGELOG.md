# Changelog

All notable changes to Zakira.Recall are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [Semantic Versioning](https://semver.org/)
while the project is pre-1.0 (minor bumps may change behaviour).

## [Unreleased]

### Fixed

- Session directories of fetches that were in flight when the Playwright driver died are now removed as soon as the
  driver loss is detected (and on host shutdown) instead of lingering until the one-hour stale sweep. Cleanup also
  deletes once more after the browser reports disconnection, catching the last file Edge writes while shutting down.

## [0.6.1] - 2026-10-01

### Fixed

- On Linux and macOS, root-relative image references (`/img/photo.jpg` in `og:image` or JSON-LD) were dropped, leaving
  `mainImage` empty: .NET parses a rooted path as an absolute `file://` URI there. References without a scheme are now
  combined with the page URL on every platform.
- `pack.ps1 -Push` falls back to the `www.nuget.org` v2 endpoint when `api.nuget.org` is unreachable.
- Test suite portability on Unix (directory-lock simulation, locale-dependent `Accept-Language` assertion).

## [0.6.0] - 2026-10-01

Page fetching was rebuilt around what agents actually need from a page: the whole readable article, its structured
data, a truthful success flag, and a browser session that recovers from failure.

### Added

- `FetchResponse.structuredData`: schema.org JSON-LD parsed from the page (`@graph`, nested `mainEntity`,
  multi-valued `@type`, malformed blocks skipped). The most relevant content entity (`Recipe`, `HowTo`, `Article`,
  `Product`, `Event`, ...) is returned inline with bulky non-content members (reviews, comments, actions) removed.
  Recipes arrive with `recipeIngredient`, `recipeInstructions`, ISO 8601 times and yield.
- `FetchResponse.mainImage` (`og:image`, `twitter:image` or the entity image, absolute URL), `statusCode` (HTTP status
  of the main document) and `contentSelector` (the element the text was taken from, for diagnosing extraction).
- `publishedAt` and `siteName` fall back to JSON-LD (`datePublished`, `publisher`) when meta tags are missing; human
  date formats such as `July 8, 2024 at 2:48pm` are parsed.
- New fetch error code `fetch_http_error` for 4xx/5xx responses (transient for 408/425/429/5xx). The page text is still
  returned so the error page can be inspected.
- `web_batch_fetch` accepts `maxConcurrentFetches`; `IFetchService.FetchBatchAsync` for library users.
- `recall fetch --output markdown` appends a "Structured data" section with the main entity as JSON.
- `CHANGELOG.md`; README sections "Fetch Output" and "Browser lifecycle".

### Changed

- Readable content is extracted in-process from the rendered DOM (AngleSharp) instead of by in-page JavaScript.
  Candidate containers (`article`, `main`, `[role=main]`, SPA roots, common CMS wrappers) are scored by
  boilerplate-free word count and link density, with a fallback to the whole body when the winner holds less than
  40% of the page's words. Previously the first `<article>` in DOM order was taken, which on many sites is a related
  post card or a comment: chainbaker.com and kingarthurbaking.com recipes came back with 70 and 137 words and no
  ingredients; they now yield 860 and 1450 words with complete ingredient lists.
- Extracted text keeps block structure (paragraphs, headings, `-` / `1.` bullets, `|`-separated table cells,
  preformatted blocks). The old `innerText` of a detached clone fused adjacent blocks into single words.
- Noise removal is scoped: an `<aside>` inside an article is kept unless it is almost entirely links (recipe
  ingredient panels), forms are no longer removed wholesale, TOC plugins, breadcrumbs, comment areas and hidden
  elements are removed.
- Challenge and block pages from Cloudflare, Akamai, PerimeterX, Imperva and Distil are recognized as
  `fetch_bot_challenge` (transient) instead of being returned as content.
- Headless fetches present the browser's regular user agent. Headless Chromium/Edge advertises
  `HeadlessChrome/<version>`, which several sites answer with a "Just a moment..." verification page; the marker is
  removed while the version is kept consistent with the `sec-ch-ua` client hints.
- Headless sessions are seeded from a cache-free copy of the profile (32.6 MB / 481 files became 1.7 MB / 186 files
  on a typical profile); cookies, logins, preferences and storage are kept.
- `web_batch_fetch` bounds concurrency by the profile's `maxConcurrentFetches` (default 3) instead of launching one
  browser per URL at once.
- MCP tool names in the README and the agent skill are the snake_case names the server actually exposes
  (`web_fetch`, not `WebFetch`); MCP result shapes are documented in camelCase.

### Fixed

- A dead Playwright driver (crashed, or killed by an external process sweep) made every later fetch in a long-lived
  MCP host fail instantly with `fetch_failed "Process exited"` until restart. The driver is now replaced and the fetch
  retried once; concurrent first use no longer spawns several drivers.
- Errors of the `PlaywrightException` family, including the driver's "Process exited", are classified as transient.
- The HTTP status of the fetched document was discarded, so 404/403/503 pages with a few sentences were reported as
  successful content.
- Headless session directories were leaked under `%TEMP%\Zakira.Recall\browser-sessions` (839 on one machine):
  deletion now waits for the browser process to exit, retries, and stale directories are swept at start-up.
- Integration tests picked a stale `Release` output folder without the tool DLL and failed on Debug builds.

### Unreleased work from May 2026 now included

The following was committed locally on 2026-05-07 as "0.4.0" but never pushed or published; it ships in 0.6.0:

- `recall eval run <dataset.json>` and `recall eval score <dataset.json> <responses.json>` to score research quality
  (expected domains, required terms, bad domains) with a markdown report and `--fail-under`; `examples/eval-dataset.json`.
- Research fetches up to three times `topPagesToRead` candidates and keeps the strongest, so a weak or failed page is
  replaced instead of reducing the result; source scoring reworked (rank-weighted, stronger penalty for weak pages).
- Fetch quality checks: `fetch_bot_challenge`, `fetch_login_required` and `fetch_weak_content` (< 15 words).
- Bing detects blocked, consent and sign-in pages and fails fast so provider fallback can take over.
- Provider fallback candidates normalized through the registry; interactive profile preparation no longer rewrites
  the saved profile.

## [0.5.0] - 2026-06-06

### Changed

- Version bump only (skill metadata and package version). Same code as 0.3.0.

## [0.4.0] - 2026-06-06

### Added

- Agent skill `skills/zakira-recall/` (SKILL.md plus references for search, research, fetch, setup,
  troubleshooting and the MCP tool surface) and a README section on installing it for OpenCode, Claude Code and
  the `.agents` convention.

Same code as 0.3.0. (A different, unpublished local build also carried the number 0.4.0; its changes are listed
under 0.6.0.)

## [0.3.0] - 2026-04-29

### Added

- `--verbose` and `--quiet` global CLI flags.

### Fixed

- DuckDuckGo HTML result parsing: a spelling-correction notice preceding the results could swallow the first
  organic result.

## [0.2.0] - 2026-04-25

### Added

- Research pipeline: domain diversity when picking top pages, result quality scoring, extractive summary.

### Changed

- Version alignment between the tool and the package; CI fixes.

## [0.1.0] - 2026-04-24

### Added

- Initial release: `recall search`, `recall fetch`, `recall research` (URL deduplication, structured citations,
  partial-error model where one failed page does not fail the run), profiles with persistent browser state,
  `recall profile auth` for interactive sign-in, DuckDuckGo (HTTP and browser) and Bing providers, provider fallback
  and health tracking, the `recall mcp` stdio server, `recall config init`, `pack.ps1` and CI workflows for Windows,
  Linux and macOS.

[Unreleased]: https://github.com/MoaidHathot/Zakira.Recall/compare/v0.6.1...HEAD
[0.6.1]: https://github.com/MoaidHathot/Zakira.Recall/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/MoaidHathot/Zakira.Recall/compare/a1427ec...v0.6.0
[0.5.0]: https://github.com/MoaidHathot/Zakira.Recall/compare/4ec7664...a1427ec
[0.4.0]: https://github.com/MoaidHathot/Zakira.Recall/compare/f6e3265...4ec7664
[0.3.0]: https://github.com/MoaidHathot/Zakira.Recall/compare/e680126...f6e3265
[0.2.0]: https://github.com/MoaidHathot/Zakira.Recall/compare/3e35057...e680126
[0.1.0]: https://github.com/MoaidHathot/Zakira.Recall/commits/3e35057
