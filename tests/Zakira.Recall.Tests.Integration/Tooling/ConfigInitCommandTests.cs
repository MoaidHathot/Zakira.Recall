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
            var projectPath = Path.Combine(GetRepositoryRoot(), "src", "Zakira.Recall.Tool", "Zakira.Recall.Tool.csproj");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" -- config init")
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
            var projectPath = Path.Combine(GetRepositoryRoot(), "src", "Zakira.Recall.Tool", "Zakira.Recall.Tool.csproj");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" -- config init --path \"{explicitPath}\"")
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

    private static string GetRepositoryRoot([CallerFilePath] string filePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, "..", "..", ".."));
}
