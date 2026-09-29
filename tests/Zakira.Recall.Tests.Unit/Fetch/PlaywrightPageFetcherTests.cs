using Zakira.Recall.Playwright.Fetch;

namespace Zakira.Recall.Tests.Unit.Fetch;

public sealed class PlaywrightPageFetcherTests
{
    private const string Target = "https://example.com/page";
    private const string UsableText = "This documentation page explains how to configure options and has plenty of words to be considered content.";

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
            "DuckDuckGo",
            "Unfortunately, bots use DuckDuckGo too. Please complete the following challenge.",
            20,
            statusCode: 200,
            "https://duckduckgo.com");

        Assert.NotNull(error);
        Assert.Equal("fetch_bot_challenge", error!.Code);
        Assert.True(error.Transient);
    }

    [Theory]
    [InlineData("Just a moment...", "www.example.com Verifying you are human. This may take a few seconds. Enable JavaScript and cookies to continue", 403)]
    [InlineData("Attention Required! | Cloudflare", "Sorry, you have been blocked. You are unable to access example.com. Why have I been blocked? Cloudflare Ray ID: 8a1b2c3d", 403)]
    [InlineData("Access Denied", "Access Denied. You don't have permission to access \"http://www.example.com/\" on this server. Reference #18.1", 403)]
    [InlineData("example.com", "Press & Hold to confirm you are a human (and not a bot). Reference ID abc", 200)]
    public void Recognizes_Cdn_Challenge_And_Block_Pages_Even_When_They_Have_Enough_Words(string title, string text, int status)
    {
        // Before: none of these matched, so a Cloudflare block page (60-120 words) came back as success=true content.
        var error = PlaywrightPageFetcher.CreateQualityError(title, text, 60, status, Target);

        Assert.NotNull(error);
        Assert.Equal("fetch_bot_challenge", error!.Code);
        Assert.True(error.Transient);
        Assert.Contains($"HTTP {status}", error.Message);
    }

    [Theory]
    [InlineData(404, false, "NotFound")]
    [InlineData(403, false, "Forbidden")]
    [InlineData(410, false, "Gone")]
    [InlineData(429, true, "TooManyRequests")]
    [InlineData(500, true, "InternalServerError")]
    [InlineData(503, true, "ServiceUnavailable")]
    [InlineData(599, true, "")]
    public void Flags_Http_Error_Statuses_Even_When_The_Error_Page_Has_Content(int status, bool transient, string statusName)
    {
        // Before: GotoAsync's response was discarded, so any error page with >= 15 words was reported as success.
        var error = PlaywrightPageFetcher.CreateQualityError("Oops", UsableText, 40, status, Target);

        Assert.NotNull(error);
        Assert.Equal("fetch_http_error", error!.Code);
        Assert.Equal(transient, error.Transient);
        Assert.Contains($"HTTP {status}", error.Message);
        if (statusName.Length > 0)
        {
            Assert.Contains(statusName, error.Message);
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(304)]
    [InlineData(null)]
    public void Accepts_Successful_Redirected_And_Unknown_Statuses(int? status)
    {
        Assert.Null(PlaywrightPageFetcher.CreateQualityError("Docs", UsableText, 40, status, Target));
    }

    [Fact]
    public void Classifies_Generic_Sign_Up_Text_As_Usable_Content()
    {
        var error = PlaywrightPageFetcher.CreateQualityError(
            "Docs",
            "This documentation page explains how to configure options. Sign up for our newsletter at the bottom of the page.",
            16,
            statusCode: 200,
            "https://example.com/docs");

        Assert.Null(error);
    }

    [Fact]
    public void Classifies_Login_Walls_As_Permanent_Errors()
    {
        var error = PlaywrightPageFetcher.CreateQualityError("LinkedIn", "Join LinkedIn to see the full post and more content like this.", 20, 200, Target);

        Assert.NotNull(error);
        Assert.Equal("fetch_login_required", error!.Code);
        Assert.False(error.Transient);
    }

    [Fact]
    public void Classifies_Thin_Content_As_Weak_Fetch_Error()
    {
        var error = PlaywrightPageFetcher.CreateQualityError("Short", "Short text only", 3, 200, Target);

        Assert.NotNull(error);
        Assert.Equal("fetch_weak_content", error!.Code);
        Assert.False(error.Transient);
    }

    [Fact]
    public void Excerpt_Is_A_Single_Line_Capped_At_400_Characters()
    {
        var text = "Headline\n\n" + string.Join('\n', Enumerable.Range(1, 200).Select(index => $"- item {index}"));

        var excerpt = PlaywrightPageFetcher.CreateExcerpt(text);

        Assert.NotNull(excerpt);
        Assert.Equal(400, excerpt!.Length);
        Assert.StartsWith("Headline - item 1 - item 2", excerpt);
        Assert.DoesNotContain('\n', excerpt);
        Assert.Null(PlaywrightPageFetcher.CreateExcerpt("  \n "));
    }
}
