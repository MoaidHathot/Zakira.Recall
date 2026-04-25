using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Zakira.Recall.Tests.Integration.Tooling;

public sealed class InspectAndOutputCommandTests
{
    [Fact]
    public async Task Config_Show_Emits_Config_As_Json()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(root.FullName, "profiles.json");
            await File.WriteAllTextAsync(configPath, """
                {
                  "defaultProvider": "bing",
                  "defaultProfile": "default",
                  "profiles": {
                    "default": {
                      "name": "default",
                      "defaultProvider": "bing",
                      "channel": "msedge",
                      "headless": true
                    }
                  }
                }
                """);

            var result = await RunToolAsync($"config show --path \"{configPath}\" --output json");

            Assert.True(result.ExitCode == 0, result.DebugText);
            Assert.Contains("\"DefaultProvider\": \"bing\"", result.StandardOutput);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Profile_Show_Resolves_Profile_As_Text()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(root.FullName, "profiles.json");
            var profilesRoot = Path.Combine(root.FullName, "profiles");
            await File.WriteAllTextAsync(configPath, string.Join(Environment.NewLine,
            [
                "{",
                "  \"defaultProvider\": \"duckduckgo\",",
                "  \"defaultProfile\": \"work\",",
                $"  \"profilesRoot\": \"{profilesRoot.Replace("\\", "\\\\")}\",",
                "  \"profiles\": {",
                "    \"work\": {",
                "      \"name\": \"work\",",
                "      \"defaultProvider\": \"bing\",",
                "      \"channel\": \"chrome\",",
                "      \"headless\": false",
                "    }",
                "  }",
                "}"
            ]));

            var result = await RunToolAsync($"--config \"{configPath}\" profile show work --output text");

            Assert.True(result.ExitCode == 0, result.DebugText);
            Assert.Contains("Profile: work", result.StandardOutput);
            Assert.Contains("Provider: bing", result.StandardOutput);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Profile_Show_Defaults_To_Text_Output()
    {
        var result = await RunToolAsync("profile show default");

        Assert.True(result.ExitCode == 0, result.DebugText);
        Assert.Contains("Profile:", result.StandardOutput);
        Assert.DoesNotContain("\"Name\"", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Providers_List_Supports_Dump_Output()
    {
        var result = await RunToolAsync("providers list --output dump");

        Assert.True(result.ExitCode == 0, result.DebugText);
        Assert.Contains("duckduckgo", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capabilities", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
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
    {
        var outputPath = GetToolOutputPath();
        var toolDllPath = Path.Combine(outputPath, "Zakira.Recall.Tool.dll");
        Assert.True(File.Exists(toolDllPath), $"Expected tool DLL '{toolDllPath}' to exist.");
        return toolDllPath;
    }

    private static string GetToolOutputPath()
    {
        var binRoot = Path.Combine(Path.GetTempPath(), "Zakira.Recall", "bin");
        var candidates = new[]
        {
            Path.Combine(binRoot, "Release", "net10.0"),
            Path.Combine(binRoot, "Debug", "net10.0")
        };

        var outputPath = candidates.FirstOrDefault(Directory.Exists);
        Assert.True(outputPath is not null, $"Expected tool output directory under '{binRoot}' to exist.");
        return outputPath!;
    }

    private static string GetRepositoryRoot([CallerFilePath] string filePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, "..", "..", ".."));

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string DebugText => $"ExitCode: {ExitCode}{Environment.NewLine}{StandardOutput}{Environment.NewLine}{StandardError}";
    }
}
