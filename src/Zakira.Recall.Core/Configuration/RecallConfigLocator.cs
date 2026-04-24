using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Infrastructure;

namespace Zakira.Recall.Core.Configuration;

public sealed class RecallConfigLocator(ISystemEnvironment environment) : IRecallConfigLocator
{
    public string GetDefaultConfigPath() => GetCandidateConfigPaths().First();

    public IReadOnlyList<string> GetCandidateConfigPaths()
    {
        var xdgConfigHome = environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            return
            [
                Path.Combine(xdgConfigHome, "Zakira.Recall", "profiles.json"),
                Path.Combine(xdgConfigHome, "Zakira.Recall.json")
            ];
        }

        var appData = environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            Path.Combine(appData, "Zakira.Recall", "profiles.json"),
            Path.Combine(appData, "Zakira.Recall.json")
        ];
    }
}
