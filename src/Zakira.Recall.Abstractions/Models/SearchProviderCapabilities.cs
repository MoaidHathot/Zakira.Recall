namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchProviderCapabilities
{
    public bool SupportsPagination { get; init; }

    public bool SupportsTimeRange { get; init; }

    public bool SupportsSafeSearch { get; init; }

    public bool RequiresBrowser { get; init; }

    public bool SupportsInteractiveSetup { get; init; }
}
