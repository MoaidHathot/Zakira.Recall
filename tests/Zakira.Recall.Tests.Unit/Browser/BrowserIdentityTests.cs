using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Unit.Browser;

public sealed class BrowserIdentityTests
{
    [Theory]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/153.0.0.0 Safari/537.36 Edg/153.0.0.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/153.0.0.0 Safari/537.36 Edg/153.0.0.0")]
    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/140.0.7339.16 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.7339.16 Safari/537.36")]
    public void Removes_The_Headless_Marker_And_Keeps_The_Real_Version(string reported, string expected)
    {
        Assert.True(BrowserIdentity.IsHeadlessUserAgent(reported));
        Assert.Equal(expected, BrowserIdentity.NormalizeUserAgent(reported));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/153.0.0.0 Safari/537.36 Edg/153.0.0.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Leaves_Regular_User_Agents_Alone(string? reported)
    {
        Assert.False(BrowserIdentity.IsHeadlessUserAgent(reported));
        Assert.Null(BrowserIdentity.NormalizeUserAgent(reported));
    }
}
