using System.Net;
using Microsoft.Playwright;
using Zakira.Recall.Core.Services;

namespace Zakira.Recall.Tests.Unit.Services;

public sealed class ServiceErrorsTests
{
    /// <summary>Stands in for Microsoft.Playwright's internal TargetClosedException.</summary>
    private sealed class DerivedPlaywrightException(string message) : PlaywrightException(message);

    [Fact]
    public void Marks_Browser_Closed_Message_As_Transient()
    {
        var error = ServiceErrors.FromException(
            "fetch_failed",
            "Target page, context or browser has been closed",
            new InvalidOperationException("Target page, context or browser has been closed"),
            target: "https://example.com");

        Assert.True(error.Transient);
        Assert.Equal("fetch_failed", error.Code);
        Assert.Equal("https://example.com", error.Target);
    }

    [Fact]
    public void Marks_Dead_Driver_Errors_As_Transient_Even_When_Thrown_As_A_Playwright_Subclass()
    {
        // Production case: web_batch_fetch reported {"message":"Process exited","transient":false} after the Node
        // driver had been killed, because the exact-type check did not match TargetClosedException.
        Assert.True(ServiceErrors.IsTransient(new DerivedPlaywrightException("Process exited")));
        Assert.True(ServiceErrors.IsTransient(new DerivedPlaywrightException("anything from the driver")));
        Assert.True(ServiceErrors.IsTransient(new InvalidOperationException("outer", new DerivedPlaywrightException("Process exited"))));
    }

    [Fact]
    public void Marks_Timeouts_Cancellations_And_Server_Errors_As_Transient()
    {
        Assert.True(ServiceErrors.IsTransient(new TimeoutException()));
        Assert.True(ServiceErrors.IsTransient(new TaskCanceledException()));
        Assert.True(ServiceErrors.IsTransient(new HttpRequestException("boom", null, HttpStatusCode.ServiceUnavailable)));
        Assert.True(ServiceErrors.IsTransient(new HttpRequestException("no status")));
    }

    [Fact]
    public void Leaves_Client_Errors_And_Unrelated_Failures_Permanent()
    {
        Assert.False(ServiceErrors.IsTransient(new HttpRequestException("not found", null, HttpStatusCode.NotFound)));
        Assert.False(ServiceErrors.IsTransient(new InvalidOperationException("Playwright runtime is not installed.")));
        Assert.False(ServiceErrors.IsTransient(new ArgumentException("bad url")));
    }
}
