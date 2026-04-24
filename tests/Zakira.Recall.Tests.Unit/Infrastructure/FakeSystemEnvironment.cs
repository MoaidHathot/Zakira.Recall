using Zakira.Recall.Core.Infrastructure;

namespace Zakira.Recall.Tests.Unit.Infrastructure;

public sealed class FakeSystemEnvironment(Dictionary<string, string?> variables, string appData, string localAppData) : ISystemEnvironment
{
    public string? GetEnvironmentVariable(string variable) => variables.TryGetValue(variable, out var value) ? value : null;

    public string GetFolderPath(Environment.SpecialFolder folder) => folder switch
    {
        Environment.SpecialFolder.ApplicationData => appData,
        Environment.SpecialFolder.LocalApplicationData => localAppData,
        _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, null)
    };
}
