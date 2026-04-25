using System.Reflection;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class BingSearchProviderQualityTests
{
    [Fact]
    public void Rejects_Off_Topic_Results_When_Query_Tokens_Are_Missing()
    {
        var method = typeof(BingSearchProvider).GetMethod("LooksRelevant", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var relevant = (bool)method!.Invoke(null,
        [
            "Moaid Hathot - Microsoft",
            "Moaid Hathot works at Microsoft.",
            "https://moaid.codes/",
            new[] { "moaid", "hathot" }
        ])!;
        var irrelevant = (bool)method.Invoke(null,
        [
            "Best gaming chair in 2026",
            "Shop ergonomic gaming chairs.",
            "https://example.com/gaming-chairs",
            new[] { "moaid", "hathot" }
        ])!;

        Assert.True(relevant);
        Assert.False(irrelevant);
    }
}
