namespace Zakira.Recall.Abstractions.Config;

public sealed class RuntimeDefaults
{
    public string? ConfigPath { get; set; }

    public string? DefaultProvider { get; set; }

    public string? DefaultProfile { get; set; }

    public string? ProfilesRoot { get; set; }

    public string? LogLevel { get; set; }
}
