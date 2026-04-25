using Zakira.Recall.Core.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class ProviderHealthTrackerTests
{
    [Fact]
    public void Marks_Provider_Unhealthy_Until_Cooldown_Expires()
    {
        var tracker = new ProviderHealthTracker();

        tracker.RecordFailure("duckduckgo");

        Assert.False(tracker.IsHealthy("duckduckgo", cooldownSeconds: 3600));
        var snapshot = tracker.GetSnapshot("duckduckgo", cooldownSeconds: 3600);
        Assert.Equal("duckduckgo", snapshot.Provider);
        Assert.Equal(1, snapshot.ConsecutiveFailures);
        Assert.NotNull(snapshot.LastFailureUtc);
    }

    [Fact]
    public void Success_Resets_Failure_Count()
    {
        var tracker = new ProviderHealthTracker();

        tracker.RecordFailure("bing");
        tracker.RecordSuccess("bing");

        Assert.True(tracker.IsHealthy("bing", cooldownSeconds: 3600));
        Assert.Equal(0, tracker.GetSnapshot("bing", cooldownSeconds: 3600).ConsecutiveFailures);
    }
}
