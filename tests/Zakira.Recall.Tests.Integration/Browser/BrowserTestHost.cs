using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zakira.Recall.Core.DependencyInjection;
using Zakira.Recall.Playwright.DependencyInjection;

namespace Zakira.Recall.Tests.Integration.Browser;

/// <summary>Shared plumbing for tests that drive the real Playwright + Edge fetch path.</summary>
internal static class BrowserTestHost
{
    /// <summary>True on Windows machines with Microsoft Edge (the default channel); other environments skip.</summary>
    public static bool IsAvailable
        => OperatingSystem.IsWindows()
            && new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                }
                .Where(root => !string.IsNullOrEmpty(root))
                .Any(root => File.Exists(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe")));

    public static ServiceProvider Build()
        => new ServiceCollection()
            .AddSingleton<ILoggerFactory, NullLoggerFactory>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddRecallCore()
            .AddRecallPlaywright()
            .BuildServiceProvider();

    /// <summary>Kills only node.exe children of this test process running the Playwright driver; nothing else on the machine.</summary>
    public static int KillPlaywrightDriverChildren()
    {
        var script =
            $"Get-CimInstance Win32_Process -Filter \"Name='node.exe'\" | " +
            $"Where-Object {{ $_.ParentProcessId -eq {Environment.ProcessId} -and $_.CommandLine -like '*run-driver*' }} | " +
            "ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Write-Output $_.ProcessId }";
        using var process = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }
}
