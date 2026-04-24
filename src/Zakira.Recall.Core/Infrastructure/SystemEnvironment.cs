namespace Zakira.Recall.Core.Infrastructure;

public interface ISystemEnvironment
{
    string? GetEnvironmentVariable(string variable);

    string GetFolderPath(Environment.SpecialFolder folder);
}

public sealed class SystemEnvironment : ISystemEnvironment
{
    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);

    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
}
