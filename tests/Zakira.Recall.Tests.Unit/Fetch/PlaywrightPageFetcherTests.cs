using Zakira.Recall.Playwright.Fetch;

namespace Zakira.Recall.Tests.Unit.Fetch;

public sealed class PlaywrightPageFetcherTests
{
    [Theory]
    [InlineData(100, 250)]
    [InlineData(500, 500)]
    [InlineData(5_000, 5_000)]
    [InlineData(30_000, 5_000)]
    public void Caps_Post_Load_Wait_Time(int inputTimeoutMs, int expectedTimeoutMs)
    {
        Assert.Equal(expectedTimeoutMs, PlaywrightPageFetcher.GetPostLoadWaitTimeoutMs(inputTimeoutMs));
    }

    [Fact]
    public void Classifies_Bot_Challenge_As_Transient_Fetch_Error()
    {
        var error = PlaywrightPageFetcher.CreateQualityError(
            "Unfortunately, bots use DuckDuckGo too. Please complete the following challenge.",
            20,
            "https://duckduckgo.com");

        Assert.NotNull(error);
        Assert.Equal("fetch_bot_challenge", error!.Code);
        Assert.True(error.Transient);
    }

    [Fact]
    public void Classifies_Generic_Sign_Up_Text_As_Usable_Content()
    {
        var error = PlaywrightPageFetcher.CreateQualityError(
            "This documentation page explains how to configure options. Sign up for our newsletter at the bottom of the page.",
            16,
            "https://example.com/docs");

        Assert.Null(error);
    }

    [Fact]
    public void Classifies_Thin_Content_As_Weak_Fetch_Error()
    {
        var error = PlaywrightPageFetcher.CreateQualityError("Short text only", 3, "https://example.com");

        Assert.NotNull(error);
        Assert.Equal("fetch_weak_content", error!.Code);
        Assert.False(error.Transient);
    }
}
