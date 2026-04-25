using System.Reflection;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class BingSearchProviderTests
{
    [Fact]
    public void Normalizes_Bing_Tracking_Urls()
    {
        var method = typeof(BingSearchProvider).GetMethod("NormalizeUrl", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = (string)method!.Invoke(null,
        [
            "https://www.bing.com/ck/a?!&&p=abc&u=a1aHR0cHM6Ly9tb2FpZC5jb2Rlcy8&ntb=1"
        ])!;

        Assert.Equal("https://moaid.codes/", result);
    }
}
