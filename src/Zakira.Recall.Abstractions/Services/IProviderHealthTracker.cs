using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IProviderHealthTracker
{
    bool IsHealthy(string providerName, int cooldownSeconds);

    void RecordSuccess(string providerName);

    void RecordFailure(string providerName);

    ProviderHealthSnapshot GetSnapshot(string providerName, int cooldownSeconds);
}
