namespace Zakira.Recall.Abstractions.Models;

public sealed class ProviderAttempt
{
    public required string Provider { get; init; }

    public bool Success { get; init; }

    public bool Skipped { get; init; }

    public int ResultCount { get; init; }

    public OperationError? Error { get; init; }
}
