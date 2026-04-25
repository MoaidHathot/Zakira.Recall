using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Core.Configuration;

namespace Zakira.Recall.Tests.Unit.Configuration;

public sealed class RecallConfigValidatorTests
{
    [Fact]
    public void Rejects_Unknown_Default_Provider()
    {
        var validator = new RecallConfigValidator();
        var config = new RecallConfig { DefaultProvider = "google" };

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(config));

        Assert.Contains("Unsupported provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_Invalid_Log_Level()
    {
        var validator = new RecallConfigValidator();
        var config = new RecallConfig { LogLevel = "loud" };

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(config));

        Assert.Contains("Unsupported log level", exception.Message, StringComparison.Ordinal);
    }
}
