using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Configuration;
using Zakira.Recall.Core.Profiles;
using Zakira.Recall.Tests.Unit.Infrastructure;

namespace Zakira.Recall.Tests.Unit.Profiles;

public sealed class ProfileResolverTests
{
    [Fact]
    public async Task Uses_Configured_Profile_Data_Directory_Override()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(tempRoot.FullName, "profiles.json");
            await File.WriteAllTextAsync(configPath, """
            {
              "defaultProfile": "work",
              "profiles": {
                "work": {
                  "defaultProvider": "bing",
                  "userDataDir": "D:/custom/work-profile",
                  "channel": "msedge"
                }
              }
            }
            """);

            var environment = new FakeSystemEnvironment(new Dictionary<string, string?>(), @"C:\Roaming", @"C:\Local");
            var runtimeDefaults = new RuntimeDefaults { ConfigPath = configPath };
            IRecallConfigLocator locator = new RecallConfigLocator(environment);
            IRecallConfigValidator validator = new RecallConfigValidator();
            IRecallConfigLoader loader = new RecallConfigLoader(locator, runtimeDefaults, validator);
            var resolver = new ProfileResolver(loader, environment);

            var profile = await resolver.ResolveAsync(null, null);

            Assert.Equal("work", profile.Name);
            Assert.Equal("bing", profile.DefaultProvider);
            Assert.Equal(Path.GetFullPath("D:/custom/work-profile"), profile.UserDataDir);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public async Task Uses_Profiles_Root_From_Runtime_Defaults_When_Present()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(tempRoot.FullName, "profiles.json");
            await File.WriteAllTextAsync(configPath, """
            {
              "profiles": {
                "personal": {
                  "defaultProvider": "duckduckgo"
                }
              }
            }
            """);

            var environment = new FakeSystemEnvironment(new Dictionary<string, string?>(), @"C:\Roaming", @"C:\Local");
            var runtimeDefaults = new RuntimeDefaults
            {
                ConfigPath = configPath,
                DefaultProfile = "personal",
                ProfilesRoot = @"D:\recall\profiles"
            };

            IRecallConfigLocator locator = new RecallConfigLocator(environment);
            IRecallConfigValidator validator = new RecallConfigValidator();
            IRecallConfigLoader loader = new RecallConfigLoader(locator, runtimeDefaults, validator);
            var resolver = new ProfileResolver(loader, environment);

            var profile = await resolver.ResolveAsync("personal", "duckduckgo");

            Assert.Equal(Path.Combine(Path.GetFullPath(@"D:\recall\profiles"), "personal"), profile.UserDataDir);
            Assert.Equal("duckduckgo", profile.DefaultProvider);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }
}
