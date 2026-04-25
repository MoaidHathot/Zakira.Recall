using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Tests.Unit.Infrastructure;

namespace Zakira.Recall.Tests.Unit.Configuration;

public sealed class RecallConfigLocatorTests
{
    [Fact]
    public void Prefers_Xdg_Config_Home_When_Set()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var xdgConfigHome = Path.Combine(root.FullName, "xdg-config");
            var appData = Path.Combine(root.FullName, "app-data");
            var localAppData = Path.Combine(root.FullName, "local-app-data");

            var environment = new FakeSystemEnvironment(
                new Dictionary<string, string?> { ["XDG_CONFIG_HOME"] = xdgConfigHome },
                appData,
                localAppData);
            var locator = new RecallConfigLocator(environment);

            var candidates = locator.GetCandidateConfigPaths();

            Assert.Equal(Path.Combine(xdgConfigHome, "Zakira.Recall", "profiles.json"), candidates[0]);
            Assert.Equal(Path.Combine(xdgConfigHome, "Zakira.Recall.json"), candidates[1]);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Falls_Back_To_AppData_When_Xdg_Not_Set()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var appData = Path.Combine(root.FullName, "app-data");
            var localAppData = Path.Combine(root.FullName, "local-app-data");

            var environment = new FakeSystemEnvironment(
                new Dictionary<string, string?>(),
                appData,
                localAppData);
            var locator = new RecallConfigLocator(environment);

            var candidates = locator.GetCandidateConfigPaths();

            Assert.Equal(Path.Combine(appData, "Zakira.Recall", "profiles.json"), candidates[0]);
            Assert.Equal(Path.Combine(appData, "Zakira.Recall.json"), candidates[1]);
        }
        finally
        {
            root.Delete(true);
        }
    }
}
