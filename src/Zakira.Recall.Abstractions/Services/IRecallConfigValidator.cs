using Zakira.Recall.Abstractions.Config;

namespace Zakira.Recall.Abstractions.Services;

public interface IRecallConfigValidator
{
    void Validate(RecallConfig config);
}
