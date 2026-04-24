namespace Zakira.Recall.Abstractions.Services;

public interface IRecallConfigLocator
{
    string GetDefaultConfigPath();

    IReadOnlyList<string> GetCandidateConfigPaths();
}
