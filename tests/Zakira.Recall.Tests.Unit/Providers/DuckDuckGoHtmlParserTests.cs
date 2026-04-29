using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class DuckDuckGoHtmlParserTests
{
    [Fact]
    public void ParseResults_Extracts_Results_From_DuckDuckGo_Html_Page()
    {
        const string html = """
        <!DOCTYPE html>
        <html>
        <body>
          <div id="links" class="results">
            <div class="result results_links results_links_deep web-result">
              <div class="links_main links_deep result__body">
                <h2 class="result__title">
                  <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Falpha&amp;rut=one">Alpha Result</a>
                </h2>
                <div class="result__extras">
                  <a class="result__url" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Falpha&amp;rut=one">example.com/alpha</a>
                </div>
                <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Falpha&amp;rut=one"> First   alpha snippet. </a>
                <div class="clear"></div>
              </div>
            </div>
            <div class="result results_links results_links_deep web-result">
              <div class="links_main links_deep result__body">
                <h2 class="result__title">
                  <a class="result__a" href="https://example.com/beta">Beta Result</a>
                </h2>
                <a class="result__url" href="https://example.com/beta">example.com/beta</a>
                <a class="result__snippet" href="https://example.com/beta">Second snippet</a>
                <div class="clear"></div>
              </div>
            </div>
          </div>
        </body>
        </html>
        """;

        var results = DuckDuckGoHtmlParser.ParseResults(html, maxResults: 10);

        Assert.Collection(results,
            first =>
            {
                Assert.Equal("Alpha Result", first.Title);
                Assert.Equal("https://example.com/alpha", first.Url);
                Assert.Equal("https://example.com/alpha", first.CanonicalUrl);
                Assert.Equal("example.com", first.Host);
                Assert.Equal("example.com/alpha", first.DisplayUrl);
                Assert.Equal("First alpha snippet.", first.Snippet);
                Assert.Equal("duckduckgo", first.Provider);
                Assert.Equal(1, first.Rank);
                Assert.Equal(1, first.RawRank);
                Assert.True(first.QualityScore > 0);
                Assert.Equal(["duckduckgo"], first.SourceProviders);
            },
            second =>
            {
                Assert.Equal("Beta Result", second.Title);
                Assert.Equal("https://example.com/beta", second.Url);
                Assert.Equal("https://example.com/beta", second.CanonicalUrl);
                Assert.Equal("example.com", second.Host);
                Assert.Equal("example.com/beta", second.DisplayUrl);
                Assert.Equal("Second snippet", second.Snippet);
                Assert.Equal(2, second.Rank);
                Assert.Equal(2, second.RawRank);
                Assert.True(second.QualityScore > 0);
                Assert.Equal(["duckduckgo"], second.SourceProviders);
            });
    }

    [Fact]
    public void ParseResults_Respects_MaxResults()
    {
        const string html = """
        <!DOCTYPE html>
        <html>
        <body>
          <div class="result web-result"><div><a class="result__a" href="https://example.com/1">One</a><div class="clear"></div></div></div>
          <div class="result web-result"><div><a class="result__a" href="https://example.com/2">Two</a><div class="clear"></div></div></div>
        </body>
        </html>
        """;

        var results = DuckDuckGoHtmlParser.ParseResults(html, maxResults: 1);

        Assert.Single(results);
        Assert.Equal("One", results[0].Title);
    }

    [Fact]
    public void ParseResults_Does_Not_Drop_First_Result_When_Preceded_By_Spelling_Correction_Block()
    {
        // Regression: a non-result div before the first organic result
        // (e.g. the "Including results for ..." notice) used to hijack the
        // outer regex and cause the first result to be silently swallowed.
        const string html = """
        <!DOCTYPE html>
        <html>
        <body>
          <div id="links" class="results">
            <div class="msg msg--spelling">
              <div id="did_you_mean">
                Including results for <a href="/html/?q=turbophase"><b>turbophase</b></a>.
              </div>
            </div>
            <div class="result results_links results_links_deep web-result ">
              <div class="links_main links_deep result__body">
                <h2 class="result__title">
                  <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fgithub.com%2FMoaidHathot%2FTurbophrase&amp;rut=x">GitHub - MoaidHathot/Turbophrase</a>
                </h2>
                <a class="result__url" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fgithub.com%2FMoaidHathot%2FTurbophrase&amp;rut=x">github.com/MoaidHathot/Turbophrase</a>
                <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fgithub.com%2FMoaidHathot%2FTurbophrase&amp;rut=x">Turbophrase AI-powered text transformation tool.</a>
                <div class="clear"></div>
              </div>
            </div>
            <div class="result results_links results_links_deep web-result ">
              <div class="links_main links_deep result__body">
                <h2 class="result__title">
                  <a class="result__a" href="https://powerphase.com/">Powerphase</a>
                </h2>
                <a class="result__url" href="https://powerphase.com/">powerphase.com</a>
                <a class="result__snippet" href="https://powerphase.com/">Gas turbine optimization.</a>
                <div class="clear"></div>
              </div>
            </div>
          </div>
        </body>
        </html>
        """;

        var results = DuckDuckGoHtmlParser.ParseResults(html, maxResults: 10);

        Assert.Equal(2, results.Count);
        Assert.Equal("GitHub - MoaidHathot/Turbophrase", results[0].Title);
        Assert.Equal("https://github.com/MoaidHathot/Turbophrase", results[0].Url);
        Assert.Equal("Powerphase", results[1].Title);
    }
}
