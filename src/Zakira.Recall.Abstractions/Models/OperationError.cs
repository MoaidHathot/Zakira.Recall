namespace Zakira.Recall.Abstractions.Models;

public sealed class OperationError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Provider { get; init; }

    public string? Target { get; init; }

    public bool Transient { get; init; }
}
