using System.Text.Json;
using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Configuration;

public sealed class RecallConfigWriter(IRecallConfigLocator locator, RuntimeDefaults runtimeDefaults, IRecallConfigValidator validator) : IRecallConfigWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async ValueTask<string> SaveAsync(RecallConfig config, string? explicitPath = null, CancellationToken cancellationToken = default)
    {
        validator.Validate(config);

        var path = explicitPath ?? runtimeDefaults.ConfigPath ?? locator.GetDefaultConfigPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken);
        return path;
    }
}
