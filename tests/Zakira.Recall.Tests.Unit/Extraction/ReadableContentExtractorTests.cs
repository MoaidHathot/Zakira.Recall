using Zakira.Recall.Core.Extraction;

namespace Zakira.Recall.Tests.Unit.Extraction;

public sealed class ReadableContentExtractorTests
{
    private static readonly ReadableContentExtractor Extractor = new();

    private static string Words(int count, string prefix = "word")
        => string.Join(' ', Enumerable.Range(1, count).Select(index => $"{prefix}{index}"));

    [Fact]
    public void Prefers_The_Largest_Article_Over_A_Preceding_Teaser_Card()
    {
        var html = $"""
            <html><body>
              <article class="card"><h3>Related post</h3><p>{Words(20)}</p></article>
              <article id="post-7"><h1>Real story</h1><p>{Words(300, "body")}</p></article>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.Equal("article#post-7", content.ContentSelector);
        Assert.Contains("body300", content.Text);
        Assert.DoesNotContain("Related post", content.Text);
    }

    [Fact]
    public void Falls_Back_To_Body_When_The_Best_Candidate_Is_A_Small_Fraction_Of_The_Page()
    {
        var html = $"""
            <html><body>
              <article class="comment"><p>{Words(30)}</p></article>
              <div class="whatever"><p>{Words(400, "story")}</p></div>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.Equal("body", content.ContentSelector);
        Assert.Contains("story400", content.Text);
    }

    [Fact]
    public void Prefers_A_Semantic_Container_Over_A_Generic_Wrapper_Of_Similar_Size()
    {
        var html = $"""
            <html><body>
              <div id="content">
                <div class="teaser">{Words(20, "teaser")}</div>
                <article><p>{Words(300, "body")}</p></article>
              </div>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.Equal("article", content.ContentSelector);
        Assert.DoesNotContain("teaser1", content.Text);
    }

    [Fact]
    public void Ignores_Candidates_That_Sit_Inside_Noise_Regions()
    {
        var html = $"""
            <html><body>
              <nav><article><p>{Words(500, "menu")}</p></article></nav>
              <main><p>{Words(100, "body")}</p></main>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.Equal("main", content.ContentSelector);
        Assert.DoesNotContain("menu1", content.Text);
    }

    [Fact]
    public void Removes_Chrome_Hidden_Elements_And_Comment_Areas()
    {
        var html = $"""
            <html><body>
              <header>SiteHeader</header>
              <nav>MenuLink</nav>
              <p id="breadcrumbs">Home » Recipes</p>
              <main>
                <p>{Words(50)}</p>
                <p hidden>HiddenAttr</p>
                <p aria-hidden="true">AriaHidden</p>
                <p style="display: none">InlineHidden</p>
                <span class="screen-reader-text">SkipToContent</span>
                <div class="wp-block-yoast-seo-table-of-contents"><h2>Table of Contents</h2></div>
                <button>ClickMe</button>
                <script>var x = "ScriptText";</script>
                <div id="comments"><article class="comment-body">CommentText</article></div>
              </main>
              <footer>FooterText</footer>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        foreach (var removed in new[] { "SiteHeader", "MenuLink", "Home »", "HiddenAttr", "AriaHidden", "InlineHidden", "SkipToContent", "Table of Contents", "ClickMe", "ScriptText", "CommentText", "FooterText" })
        {
            Assert.DoesNotContain(removed, content.Text);
        }

        Assert.Contains("word50", content.Text);
    }

    [Fact]
    public void Keeps_A_Content_Aside_Inside_An_Article_But_Drops_Link_Heavy_Or_Trivial_Asides()
    {
        var html = $"""
            <html><body>
              <article>
                <p>{Words(200)}</p>
                <aside class="ingredients"><ul>
                  <li>2 cups <a href="/flour">bread flour</a></li>
                  <li>1 tsp <a href="/salt">fine sea salt</a></li>
                  <li>1 cup <a href="/water">lukewarm water</a></li>
                  <li>2 tsp <a href="/yeast">instant yeast</a></li>
                </ul></aside>
                <aside class="promo"><a href="/chat">Chat with a baker</a></aside>
              </article>
              <aside class="widget"><ul>
                <li><a href="/a">Popular post one</a></li><li><a href="/b">Popular post two</a></li>
                <li><a href="/c">Popular post three</a></li><li><a href="/d">Popular post four</a></li>
              </ul></aside>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.Contains("- 2 cups bread flour", content.Text);
        Assert.Contains("- 2 tsp instant yeast", content.Text);
        Assert.DoesNotContain("Chat with a baker", content.Text);
        Assert.DoesNotContain("Popular post", content.Text);
    }

    [Fact]
    public void Separates_Blocks_With_Line_Breaks_And_Formats_Lists_And_Tables()
    {
        var html =
            "<html><body><main>" +
            "<h2>Title</h2><p>First paragraph.</p><p>Second<br>line</p>" +
            "<ul><li>alpha</li><li>beta</li></ul>" +
            "<ol><li>one</li><li>two</li></ol>" +
            "<table><tr><th>Name</th><th>Value</th></tr><tr><td>Calories</td><td>117</td></tr></table>" +
            "<pre>keep   this\n    indented</pre>" +
            "<span>inline</span><b>bold</b>" +
            "</main></body></html>";

        var content = Extractor.Extract(html);

        var expected = string.Join('\n',
            "Title",
            "",
            "First paragraph.",
            "",
            "Second",
            "line",
            "",
            "- alpha",
            "- beta",
            "",
            "1. one",
            "2. two",
            "",
            "Name | Value",
            "Calories | 117",
            "",
            "keep   this",
            "    indented",
            "",
            "inlinebold");
        Assert.Equal(expected, content.Text);
    }

    [Fact]
    public void Keeps_A_Bullet_On_The_Same_Line_As_Text_Wrapped_In_A_Block_And_Drops_Empty_Items()
    {
        var html = """
            <html><body><main>
              <p>Intro paragraph with enough words to count as content for the test.</p>
              <ul>
                <li><figure><img src="a.jpg"></figure></li>
                <li><p>3 pounds potatoes</p></li>
                <li><div><span>½ teaspoon</span> <span>baking soda</span></div></li>
              </ul>
            </main></body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.Contains("\n- 3 pounds potatoes\n", content.Text);
        Assert.EndsWith("- ½ teaspoon baking soda", content.Text);
        Assert.DoesNotContain("- \n", content.Text);
        Assert.DoesNotContain("-  ", content.Text);
    }

    [Fact]
    public void Reads_Title_Headline_Description_Site_Name_And_Published_Time()
    {
        var html = $"""
            <html><head>
              <title>  Page   Title </title>
              <meta property="og:description" content="Open graph description">
              <meta property="og:site_name" content="Example Site">
              <meta property="article:published_time" content="2024-05-01T10:00:00+02:00">
            </head><body>
              <main><h2>Second-level headline</h2><p>{Words(30)}</p></main>
            </body></html>
            """;

        var content = Extractor.Extract(html, "https://www.example.com/post");

        Assert.Equal("Page Title", content.Title);
        Assert.Equal("Second-level headline", content.Headline);
        Assert.Equal("Open graph description", content.MetaDescription);
        Assert.Equal("Example Site", content.SiteName);
        Assert.Equal(DateTimeOffset.Parse("2024-05-01T10:00:00+02:00"), content.PublishedAt);
        Assert.StartsWith("Second-level headline\n\nOpen graph description\n\nword1", content.Text);
    }

    [Fact]
    public void Falls_Back_To_Host_Name_And_Time_Element_For_Metadata()
    {
        var html = $"""
            <html><head><title>T</title></head><body>
              <main><h1>Headline</h1><p>Posted <time datetime="2023-01-02">Jan 2</time></p><p>{Words(30)}</p></main>
            </body></html>
            """;

        var content = Extractor.Extract(html, "https://blog.example.org/x");

        Assert.Equal("blog.example.org", content.SiteName);
        Assert.Equal(new DateTimeOffset(2023, 1, 2, 0, 0, 0, TimeSpan.Zero), content.PublishedAt);
        Assert.Null(content.MetaDescription);
    }

    [Fact]
    public void Shows_The_Headline_Once_And_Skips_A_Description_The_Text_Already_Contains()
    {
        var html = """
            <html><head><meta name="description" content="A summary sentence that also opens the article."></head><body>
              <main>
                <div class="category">Breads</div>
                <h1>My Headline</h1>
                <p>A summary sentence that also opens the article. And then it continues with more words here.</p>
              </main>
            </body></html>
            """;

        var content = Extractor.Extract(html);

        Assert.StartsWith("My Headline\n\nBreads\n\nA summary sentence", content.Text);
        Assert.Equal(1, content.Text.Split("My Headline").Length - 1);
        Assert.Equal(1, content.Text.Split("A summary sentence").Length - 1);
    }

    [Fact]
    public void Handles_Empty_And_Text_Only_Documents()
    {
        var empty = Extractor.Extract(string.Empty);
        Assert.Equal(string.Empty, empty.Text);
        Assert.Equal(0, empty.WordCount);

        var textOnly = Extractor.Extract("just some words here");
        Assert.Equal("just some words here", textOnly.Text);
        Assert.Equal(4, textOnly.WordCount);
        Assert.Equal("body", textOnly.ContentSelector);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("one two three", 3)]
    [InlineData("- | ▢ …", 0)]
    [InlineData("½ cup", 2)]
    [InlineData("a\nb\tc  d", 4)]
    public void Counts_Only_Tokens_That_Contain_Letters_Or_Digits(string text, int expected)
    {
        Assert.Equal(expected, ReadableContentExtractor.CountWords(text));
    }

    [Fact]
    public void Describes_Elements_With_Tag_Id_And_Up_To_Two_Classes()
    {
        var html = "<html><body><main id='m' class='one two three'></main></body></html>";
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Equal("main#m.one.two", ReadableContentExtractor.Describe(document.QuerySelector("main")!));
        Assert.Equal("body", ReadableContentExtractor.Describe(document.Body!));
    }
}
