using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IProfileBootstrapper
{
    ValueTask<ProfileDescriptor> EnsureProfileAsync(string profileName, string? channel, string? provider, bool? headless, string? userDataDir, CancellationToken cancellationToken = default);
}
