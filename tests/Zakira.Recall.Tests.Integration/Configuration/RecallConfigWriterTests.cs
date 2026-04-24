using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Tests.Unit.Infrastructure;

namespace Zakira.Recall.Tests.Integration.Configuration;

public sealed class RecallConfigWriterTests
{
    [Fact]
    public async Task Saves_Config_To_Default_Location_When_Path_Not_Specified()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var environment = new FakeSystemEnvironment(
                new Dictionary<string, string?> { ["XDG_CONFIG_HOME"] = root.FullName },
                @"C:\Roaming",
                @"C:\Local");
            var locator = new RecallConfigLocator(environment);
            var writer = new RecallConfigWriter(locator, new RuntimeDefaults());

            var path = await writer.SaveAsync(new RecallConfig
            {
                DefaultProfile = "personal",
                Profiles = new Dictionary<string, RecallProfileConfig>
                {
                    ["personal"] = new() { DefaultProvider = "duckduckgo" }
                }
            });

            Assert.Equal(Path.Combine(root.FullName, "Zakira.Recall", "profiles.json"), path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
