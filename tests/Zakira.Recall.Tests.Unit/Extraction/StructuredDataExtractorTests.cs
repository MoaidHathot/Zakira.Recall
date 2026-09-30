using System.Text.Json;
using AngleSharp.Html.Parser;
using Zakira.Recall.Core.Extraction;

namespace Zakira.Recall.Tests.Unit.Extraction;

public sealed class StructuredDataExtractorTests
{
    private static readonly ReadableContentExtractor Extractor = new();

    private static StructuredDataExtractor.Result Extract(string html)
        => new StructuredDataExtractor().Extract(new HtmlParser().ParseDocument(html));

    private static string Page(string jsonLd, string body = "<main><h1>Title</h1><p>Some words on the page for the body.</p></main>")
        => $"<html><head><script type=\"application/ld+json\">{jsonLd}</script></head><body>{body}</body></html>";

    [Fact]
    public void Picks_The_Recipe_Out_Of_A_Graph_And_Prunes_Bulky_Members()
    {
        var result = Extract(Page("""
            {"@context":"https://schema.org","@graph":[
              {"@type":"WebSite","name":"Example Kitchen"},
              {"@type":"WebPage","datePublished":"2020-01-01T00:00:00+00:00"},
              {"@type":"Recipe","name":"Buns","datePublished":"2022-02-02T16:00:00+00:00",
               "recipeIngredient":["50g flour","50g water"],
               "recipeInstructions":[{"@type":"HowToStep","text":"Mix."}],
               "review":[{"@type":"Review","reviewBody":"Great!"}],
               "image":["https://img.example/a.jpg","https://img.example/b.jpg"]}
            ]}
            """));

        Assert.Equal(["WebSite", "WebPage", "Recipe"], result.Types);
        Assert.Equal("Recipe", result.MainEntityType);
        Assert.NotNull(result.MainEntity);
        Assert.Equal(2, result.MainEntity!["recipeIngredient"]!.AsArray().Count);
        Assert.False(result.MainEntity.ContainsKey("review"));
        Assert.False(result.MainEntity.ContainsKey("@context"));
        Assert.Equal("Buns", result.Headline);
        Assert.Equal("2022-02-02T16:00:00+00:00", result.DatePublished);
        Assert.Equal("Example Kitchen", result.PublisherName);
        Assert.Equal("https://img.example/a.jpg", result.ImageUrl);
    }

    [Fact]
    public void Finds_A_Nested_Main_Entity_And_Handles_Multi_Valued_Types()
    {
        var result = Extract(Page("""
            {"@type":"WebPage","mainEntity":{"@type":["Article","NewsArticle"],"headline":"Nested",
              "publisher":{"@type":"Organization","name":"The Daily"},"image":{"@type":"ImageObject","url":"/img/hero.jpg"}}}
            """));

        Assert.Equal(["WebPage"], result.Types);
        Assert.Equal("Article", result.MainEntityType);
        Assert.Equal("Nested", result.Headline);
        Assert.Equal("The Daily", result.PublisherName);
        Assert.Equal("/img/hero.jpg", result.ImageUrl);
    }

    [Fact]
    public void Prefers_Content_Types_By_Priority_And_Ignores_Site_Chrome()
    {
        var chromeOnly = Extract(Page("""[{"@type":"WebSite","name":"S"},{"@type":"BreadcrumbList"},{"@type":"Organization","name":"Org"}]"""));
        Assert.Equal(["WebSite", "BreadcrumbList", "Organization"], chromeOnly.Types);
        Assert.Null(chromeOnly.MainEntityType);
        Assert.Null(chromeOnly.MainEntity);
        Assert.Equal("S", chromeOnly.PublisherName);

        var articleAndProduct = Extract(Page("""[{"@type":"Product","name":"P"},{"@type":"BlogPosting","headline":"B"}]"""));
        Assert.Equal("BlogPosting", articleAndProduct.MainEntityType);
    }

    [Fact]
    public void Skips_Malformed_Blocks_And_Tolerates_Trailing_Commas()
    {
        var html = "<html><head>" +
            "<script type=\"application/ld+json\">{not json at all</script>" +
            "<script type=\"application/ld+json\">{\"@type\":\"Recipe\",\"name\":\"OK\",}</script>" +
            "</head><body></body></html>";

        var result = Extract(html);

        Assert.Equal(["Recipe"], result.Types);
        Assert.Equal("OK", result.Headline);
    }

    [Fact]
    public void Returns_Nothing_For_Pages_Without_Json_Ld()
    {
        var result = Extract("<html><body><p>plain</p></body></html>");

        Assert.Empty(result.Types);
        Assert.Null(result.ToModel());
    }

    [Fact]
    public void Omits_An_Oversized_Main_Entity_But_Keeps_Its_Type()
    {
        var hugeText = new string('x', StructuredDataExtractor.MaxMainEntityJsonLength + 100);
        var result = Extract(Page($$"""{"@type":"Article","headline":"Big","articleBody":"{{hugeText}}"}"""));

        var model = result.ToModel();
        Assert.NotNull(model);
        Assert.Equal("Article", model!.MainEntityType);
        Assert.Null(model.MainEntity);
        Assert.Equal("Big", result.Headline);
    }

    [Theory]
    [InlineData("\"https://a/b.jpg\"", "https://a/b.jpg")]
    [InlineData("{\"@type\":\"ImageObject\",\"contentUrl\":\"https://a/c.jpg\"}", "https://a/c.jpg")]
    [InlineData("[{\"@type\":\"ImageObject\",\"url\":\"https://a/d.jpg\"},\"https://a/e.jpg\"]", "https://a/d.jpg")]
    [InlineData("42", null)]
    public void Reads_Every_Schema_Org_Image_Shape(string imageJson, string? expected)
    {
        var result = Extract(Page($$"""{"@type":"Article","image":{{imageJson}}}"""));

        Assert.Equal(expected, result.ImageUrl);
    }

    [Fact]
    public void Readable_Extractor_Exposes_Structured_Data_And_Backfills_Metadata_From_It()
    {
        var html = Page("""
            {"@type":"Recipe","name":"Loaf","datePublished":"July 8, 2024 at 2:48pm",
             "publisher":{"@type":"Organization","name":"Flour Co"},"image":"/files/loaf.jpg","recipeIngredient":["flour"]}
            """);

        var content = Extractor.Extract(html, "https://www.flour.example/recipes/loaf");

        Assert.NotNull(content.StructuredData);
        Assert.Equal("Recipe", content.StructuredData!.MainEntityType);
        Assert.Equal(JsonValueKind.Object, content.StructuredData.MainEntity!.Value.ValueKind);
        Assert.Equal("flour", content.StructuredData.MainEntity.Value.GetProperty("recipeIngredient")[0].GetString());
        Assert.Equal(new DateTimeOffset(2024, 7, 8, 14, 48, 0, TimeSpan.Zero), content.PublishedAt);
        Assert.Equal("Flour Co", content.SiteName);
        Assert.Equal("https://www.flour.example/files/loaf.jpg", content.MainImage);
    }

    [Fact]
    public void Meta_Tags_Take_Precedence_Over_Json_Ld_For_Site_Name_Date_And_Image()
    {
        var html = "<html><head>" +
            "<meta property=\"og:site_name\" content=\"Meta Site\">" +
            "<meta property=\"article:published_time\" content=\"2021-01-01T00:00:00Z\">" +
            "<meta property=\"og:image\" content=\"https://meta.example/og.jpg\">" +
            "<script type=\"application/ld+json\">{\"@type\":\"Article\",\"datePublished\":\"2022-02-02\",\"publisher\":{\"name\":\"LD Site\"},\"image\":\"https://ld.example/ld.jpg\"}</script>" +
            "</head><body><main><h1>H</h1><p>Body words here for the extractor to read.</p></main></body></html>";

        var content = Extractor.Extract(html, "https://example.org/");

        Assert.Equal("Meta Site", content.SiteName);
        Assert.Equal(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero), content.PublishedAt);
        Assert.Equal("https://meta.example/og.jpg", content.MainImage);
    }

    [Theory]
    [InlineData("2022-02-02T16:00:00+00:00", "2022-02-02T16:00:00+00:00")]
    [InlineData("2024-05-01T10:00:00+02:00", "2024-05-01T10:00:00+02:00")]
    [InlineData("2023-01-02", "2023-01-02T00:00:00+00:00")]
    [InlineData("July 8, 2024 at 2:48pm", "2024-07-08T14:48:00+00:00")]
    [InlineData("  2019-11-20T00:00:00Z ", "2019-11-20T00:00:00+00:00")]
    public void Parses_Iso_And_Human_Publication_Dates(string input, string expected)
    {
        Assert.Equal(DateTimeOffset.Parse(expected), DateParsing.TryParse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("Posted by admin")]
    public void Rejects_Unparseable_Dates(string? input)
    {
        Assert.Null(DateParsing.TryParse(input));
    }

    [Theory]
    [InlineData("https://a.example/x.jpg", "https://b.example/", "https://a.example/x.jpg")]
    [InlineData("HTTP://a.example/x.jpg", "https://b.example/", "http://a.example/x.jpg")]
    [InlineData("/img/x.jpg", "https://b.example/post/1", "https://b.example/img/x.jpg")]
    [InlineData("img/x.jpg", "https://b.example/post/1", "https://b.example/post/img/x.jpg")]
    [InlineData("//cdn.example/x.jpg", "https://b.example/post/1", "https://cdn.example/x.jpg")]
    [InlineData("?img=1", "https://b.example/post/1", "https://b.example/post/1?img=1")]
    [InlineData("data:image/png;base64,AAAA", "https://b.example/", null)]
    [InlineData("javascript:void(0)", "https://b.example/", null)]
    [InlineData("ftp://a.example/x.jpg", "https://b.example/", null)]
    [InlineData("/img/x.jpg", null, null)]
    [InlineData("/img/x.jpg", "file:///tmp/page.html", null)]
    [InlineData("   ", "https://b.example/", null)]
    public void Resolves_Image_Urls_To_Absolute_Http(string? value, string? baseUrl, string? expected)
    {
        // "/img/x.jpg" must resolve the same way on every OS: on Unix, .NET parses a rooted path as an absolute file URI
        // when asked for UriKind.Absolute, which used to drop every root-relative image on Linux and macOS.
        Assert.Equal(expected, ReadableContentExtractor.ResolveUrl(value, baseUrl));
    }
}
