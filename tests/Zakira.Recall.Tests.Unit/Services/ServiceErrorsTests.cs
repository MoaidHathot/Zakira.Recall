using System.Reflection;

namespace Zakira.Recall.Tests.Unit.Services;

public sealed class ServiceErrorsTests
{
    [Fact]
    public void Marks_Browser_Closed_Message_As_Transient()
    {
        var serviceErrorsType = typeof(Zakira.Recall.Core.Services.SearchService).Assembly
            .GetType("Zakira.Recall.Core.Services.ServiceErrors", throwOnError: true)!;
        var method = serviceErrorsType.GetMethod("FromException", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        var error = (Zakira.Recall.Abstractions.Models.OperationError)method.Invoke(null,
        [
            "fetch_failed",
            "Target page, context or browser has been closed",
            new InvalidOperationException("Target page, context or browser has been closed"),
            null,
            "https://example.com"
        ])!;

        Assert.True(error.Transient);
    }
}
