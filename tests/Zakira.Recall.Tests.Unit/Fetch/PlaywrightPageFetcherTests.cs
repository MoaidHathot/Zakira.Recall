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
}
