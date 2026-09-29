using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Zakira.Recall.Tests.Integration.Tooling;

public sealed class EvalCommandTests
{
    [Fact]
    public async Task Eval_Score_Emits_Markdown_Report_For_Recorded_Responses()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var datasetPath = Path.Combine(root.FullName, "dataset.json");
            var responsesPath = Path.Combine(root.FullName, "responses.json");
            var reportPath = Path.Combine(root.FullName, "report.md");
            await File.WriteAllTextAsync(datasetPath, """
                {
                  "name": "offline-test",
                  "cases": [
                    {
                      "id": "case-1",
                      "query": "playwright dotnet docs",
                      "expectedDomains": ["playwright.dev"],
                      "requiredTerms": ["BrowserContext"],
                      "badDomains": ["facebook.com"]
                    }
                  ]
                }
                """);
            await File.WriteAllTextAsync(responsesPath, """
                {
                  "responses": [
                    {
                      "id": "case-1",
                      "query": "playwright dotnet docs",
                      "response": {
                        "query": "playwright dotnet docs",
                        "provider": "bing",
                        "profile": "default",
                        "success": true,
                        "summary": "[src-1] BrowserContext represents an isolated browser session in Playwright.",
                        "searchResults": [
                          {
                            "title": "BrowserContext | Playwright .NET",
                            "url": "https://playwright.dev/dotnet/docs/api/class-browsercontext",
                            "snippet": "BrowserContext documentation for Playwright .NET.",
                            "provider": "bing",
                            "rank": 1
                          }
                        ],
                        "sources": [
                          {
                            "citationId": "src-1",
                            "searchResult": {
                              "title": "BrowserContext | Playwright .NET",
                              "url": "https://playwright.dev/dotnet/docs/api/class-browsercontext",
                              "snippet": "BrowserContext documentation for Playwright .NET.",
                              "provider": "bing",
                              "rank": 1
                            },
                            "fetch": {
                              "url": "https://playwright.dev/dotnet/docs/api/class-browsercontext",
                              "finalUrl": "https://playwright.dev/dotnet/docs/api/class-browsercontext",
                              "success": true,
                              "title": "BrowserContext | Playwright .NET",
                              "text": "BrowserContext represents an isolated browser session in Playwright for .NET tests and automation.",
                              "excerpt": "BrowserContext represents an isolated browser session in Playwright for .NET tests and automation.",
                              "domain": "playwright.dev",
                              "wordCount": 15
                            }
                          }
                        ],
                        "citations": [
                          {
                            "id": "src-1",
                            "title": "BrowserContext | Playwright .NET",
                            "url": "https://playwright.dev/dotnet/docs/api/class-browsercontext",
                            "domain": "playwright.dev",
                            "provider": "bing",
                            "rank": 1,
                            "quote": "BrowserContext represents an isolated browser session in Playwright."
                          }
                        ],
                        "errors": []
                      }
                    }
                  ]
                }
                """);

            var result = await RunToolAsync($"eval score \"{datasetPath}\" \"{responsesPath}\" --output markdown --report \"{reportPath}\" --fail-under 90");

            Assert.True(result.ExitCode == 0, result.DebugText);
            Assert.Contains("# Recall Evaluation: offline-test", result.StandardOutput);
            Assert.Contains("case-1", result.StandardOutput);
            Assert.Contains("`100/100`", result.StandardOutput);
            Assert.True(File.Exists(reportPath));
            var report = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("Expected domain best rank: `1`", report);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Eval_Score_Returns_Non_Zero_When_FailUnder_Is_Not_Met()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var datasetPath = Path.Combine(root.FullName, "dataset.json");
            var responsesPath = Path.Combine(root.FullName, "responses.json");
            await File.WriteAllTextAsync(datasetPath, """
                {
                  "name": "offline-test",
                  "cases": [
                    {
                      "id": "case-1",
                      "query": "playwright dotnet docs",
                      "expectedDomains": ["playwright.dev"],
                      "requiredTerms": ["BrowserContext"]
                    }
                  ]
                }
                """);
            await File.WriteAllTextAsync(responsesPath, """
                {
                  "responses": []
                }
                """);

            var result = await RunToolAsync($"eval score \"{datasetPath}\" \"{responsesPath}\" --output text --fail-under 1");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Average score: 0", result.StandardOutput);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static async Task<CommandResult> RunToolAsync(string arguments)
    {
        var toolDllPath = GetToolDllPath();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", $"\"{toolDllPath}\" {arguments}")
            {
                WorkingDirectory = GetRepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static string GetToolDllPath()
        => ToolOutputLocator.GetToolDllPath();

    private static string GetRepositoryRoot([CallerFilePath] string filePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, "..", "..", ".."));

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string DebugText => $"ExitCode: {ExitCode}{Environment.NewLine}{StandardOutput}{Environment.NewLine}{StandardError}";
    }
}
