using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Tests.Unit.Infrastructure;

namespace Zakira.Recall.Tests.Unit.Configuration;

public sealed class RecallConfigLocatorTests
{
    [Fact]
    public void Prefers_Xdg_Config_Home_When_Set()
    {
        var environment = new FakeSystemEnvironment(
            new Dictionary<string, string?> { ["XDG_CONFIG_HOME"] = @"D:\xdg-config" },
            @"C:\Users\me\AppData\Roaming",
            @"C:\Users\me\AppData\Local");
        var locator = new RecallConfigLocator(environment);

        var candidates = locator.GetCandidateConfigPaths();

        Assert.Equal(@"D:\xdg-config\Zakira.Recall\profiles.json", candidates[0]);
        Assert.Equal(@"D:\xdg-config\Zakira.Recall.json", candidates[1]);
    }

    [Fact]
    public void Falls_Back_To_AppData_When_Xdg_Not_Set()
    {
        var environment = new FakeSystemEnvironment(
            new Dictionary<string, string?>(),
            @"C:\Users\me\AppData\Roaming",
            @"C:\Users\me\AppData\Local");
        var locator = new RecallConfigLocator(environment);

        var candidates = locator.GetCandidateConfigPaths();

        Assert.Equal(@"C:\Users\me\AppData\Roaming\Zakira.Recall\profiles.json", candidates[0]);
        Assert.Equal(@"C:\Users\me\AppData\Roaming\Zakira.Recall.json", candidates[1]);
    }
}
