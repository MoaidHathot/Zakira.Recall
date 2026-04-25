using System.Collections.Concurrent;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Providers;

public sealed class ProviderHealthTracker : IProviderHealthTracker
{
    private readonly ConcurrentDictionary<string, ProviderState> _states = new(StringComparer.OrdinalIgnoreCase);

    public bool IsHealthy(string providerName, int cooldownSeconds)
    {
        var snapshot = GetSnapshot(providerName, cooldownSeconds);
        return snapshot.IsHealthy;
    }

    public void RecordSuccess(string providerName)
    {
        var state = _states.GetOrAdd(providerName, static _ => new ProviderState());
        lock (state)
        {
            state.ConsecutiveFailures = 0;
            state.LastSuccessUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordFailure(string providerName)
    {
        var state = _states.GetOrAdd(providerName, static _ => new ProviderState());
        lock (state)
        {
            state.ConsecutiveFailures++;
            state.LastFailureUtc = DateTimeOffset.UtcNow;
        }
    }

    public ProviderHealthSnapshot GetSnapshot(string providerName, int cooldownSeconds)
    {
        if (!_states.TryGetValue(providerName, out var state))
        {
            return new ProviderHealthSnapshot
            {
                Provider = providerName,
                IsHealthy = true,
                ConsecutiveFailures = 0
            };
        }

        lock (state)
        {
            var healthy = state.ConsecutiveFailures == 0
                || state.LastFailureUtc is null
                || DateTimeOffset.UtcNow - state.LastFailureUtc.Value >= TimeSpan.FromSeconds(cooldownSeconds);

            return new ProviderHealthSnapshot
            {
                Provider = providerName,
                IsHealthy = healthy,
                ConsecutiveFailures = state.ConsecutiveFailures,
                LastSuccessUtc = state.LastSuccessUtc,
                LastFailureUtc = state.LastFailureUtc
            };
        }
    }

    private sealed class ProviderState
    {
        public int ConsecutiveFailures { get; set; }

        public DateTimeOffset? LastSuccessUtc { get; set; }

        public DateTimeOffset? LastFailureUtc { get; set; }
    }
}
