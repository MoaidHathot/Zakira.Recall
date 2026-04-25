namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchProviderDescriptor
{
    public required string Name { get; init; }

    public required SearchProviderCapabilities Capabilities { get; init; }

    public ProviderHealthSnapshot? Health { get; init; }
}
