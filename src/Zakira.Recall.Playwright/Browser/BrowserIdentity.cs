namespace Zakira.Recall.Playwright.Browser;

/// <summary>
/// The user-agent string a headless Chromium/Edge reports contains a <c>HeadlessChrome</c> product token although it is
/// the same browser build; many sites serve reduced pages or a verification interstitial to that token. The fetcher
/// therefore presents the browser's own user agent with the marker removed, keeping the real version so it stays
/// consistent with the <c>sec-ch-ua</c> client hints the browser sends.
/// </summary>
internal static class BrowserIdentity
{
    private const string HeadlessProduct = "HeadlessChrome/";
    private const string Product = "Chrome/";

    /// <summary>True when the user agent advertises headless mode.</summary>
    public static bool IsHeadlessUserAgent(string? userAgent)
        => userAgent is not null && userAgent.Contains(HeadlessProduct, StringComparison.Ordinal);

    /// <summary>
    /// Returns the user agent the same browser would send when not headless, or null when no change is needed.
    /// </summary>
    public static string? NormalizeUserAgent(string? userAgent)
    {
        if (!IsHeadlessUserAgent(userAgent))
        {
            return null;
        }

        return userAgent!.Replace(HeadlessProduct, Product, StringComparison.Ordinal);
    }
}
