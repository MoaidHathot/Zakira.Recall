namespace Zakira.Recall.Tests.Integration.Tooling;

public sealed class ToolBuildOutputTests
{
    [Fact]
    public async Task Tool_Build_Copies_Playwright_Runtime_Files()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var outputPath = ToolOutputLocator.GetToolOutputPath();
        Assert.True(File.Exists(Path.Combine(outputPath, "playwright.ps1")), "Expected playwright.ps1 to be copied to the tool output.");
        Assert.True(
            File.Exists(Path.Combine(outputPath, ".playwright", "node", "win32_x64", "node.exe")),
            "Expected the Playwright driver runtime to be copied to the tool output.");
    }
}
