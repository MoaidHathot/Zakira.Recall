using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Zakira.Recall.Tests.Integration.Tooling;

public sealed class ConfigInitCommandTests
{
    [Fact]
    public async Task Config_Init_Writes_To_Default_Config_Path_When_XdgConfigHome_Is_Set()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var configHome = Path.Combine(root.FullName, "xdg-config");
            var expectedPath = Path.Combine(configHome, "Zakira.Recall", "profiles.json");
            var toolDllPath = GetToolDllPath();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"\"{toolDllPath}\" config init")
                {
                    WorkingDirectory = GetRepositoryRoot(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.Environment["XDG_CONFIG_HOME"] = configHome;

            process.Start();
            var standardOutput = await process.StandardOutput.ReadToEndAsync();
            var standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"config init failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
            Assert.Equal(expectedPath, standardOutput.Trim());
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Config_Init_Respects_Explicit_Path()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var explicitPath = Path.Combine(root.FullName, "custom", "profiles.json");
            var toolDllPath = GetToolDllPath();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"\"{toolDllPath}\" config init --path \"{explicitPath}\"")
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

            Assert.True(process.ExitCode == 0, $"config init failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
            Assert.Equal(explicitPath, standardOutput.Trim());
            Assert.True(File.Exists(explicitPath));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Providers_List_Returns_Capabilities_As_Json()
    {
        var toolDllPath = GetToolDllPath();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", $"\"{toolDllPath}\" providers list --output json")
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

        Assert.True(process.ExitCode == 0, $"providers list failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
        Assert.Contains("duckduckgo", standardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capabilities", standardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Providers_Test_Resolves_Alias_And_Shows_Text_By_Default()
    {
        var toolDllPath = GetToolDllPath();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", $"\"{toolDllPath}\" providers test ddg")
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

        Assert.True(process.ExitCode == 0, $"providers test failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
        Assert.Contains("Provider: duckduckgo", standardOutput);
        Assert.Contains("Aliases: ddg", standardOutput);
    }

    private static string GetToolDllPath()
    {
        var binRoot = Path.Combine(Path.GetTempPath(), "Zakira.Recall", "bin");
        var candidates = new[]
        {
            Path.Combine(binRoot, "Release", "net10.0", "Zakira.Recall.Tool.dll"),
            Path.Combine(binRoot, "Debug", "net10.0", "Zakira.Recall.Tool.dll")
        };

        var toolDllPath = candidates.FirstOrDefault(File.Exists);
        Assert.True(toolDllPath is not null, $"Expected tool DLL under '{binRoot}' to exist.");
        return toolDllPath!;
    }

    private static string GetRepositoryRoot([CallerFilePath] string filePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, "..", "..", ".."));
}
