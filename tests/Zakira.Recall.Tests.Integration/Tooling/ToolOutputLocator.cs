namespace Zakira.Recall.Tests.Integration.Tooling;

/// <summary>
/// Locates the built CLI (Zakira.Recall.Tool.dll) for process-based integration tests.
/// All projects share one output root (%TEMP%\Zakira.Recall\bin\{Configuration}\net10.0, see Directory.Build.props),
/// so the directory this test assembly runs from is the tool output directory for the configuration under test.
/// A stale sibling configuration folder (e.g. a Release folder left behind by pack.ps1 without the DLL) must never win,
/// so every candidate is validated by the presence of the tool DLL rather than by the directory merely existing.
/// </summary>
internal static class ToolOutputLocator
{
    public const string ToolDllName = "Zakira.Recall.Tool.dll";

    public static string GetToolOutputPath()
    {
        var binRoot = Path.Combine(Path.GetTempPath(), "Zakira.Recall", "bin");
        var candidates = new[]
        {
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.Combine(binRoot, "Release", "net10.0"),
            Path.Combine(binRoot, "Debug", "net10.0")
        };

        var outputPath = candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, ToolDllName)));
        Assert.True(
            outputPath is not null,
            $"Expected '{ToolDllName}' in one of: {string.Join(", ", candidates)}. Build the solution (dotnet build) before running integration tests.");
        return outputPath!;
    }

    public static string GetToolDllPath()
        => Path.Combine(GetToolOutputPath(), ToolDllName);
}
