namespace Zakira.Recall.Abstractions.Models;

public sealed class ProviderHealthSnapshot
{
    public required string Provider { get; init; }

    public bool IsHealthy { get; init; }

    public int ConsecutiveFailures { get; init; }

    public DateTimeOffset? LastSuccessUtc { get; init; }

    public DateTimeOffset? LastFailureUtc { get; init; }
}
