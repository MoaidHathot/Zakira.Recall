using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Core.Extraction;

namespace Zakira.Recall.Tests.Unit.Extraction;

/// <summary>
/// Regression suite over real pages. Before the scoring extractor, the fetcher took the first
/// <c>&lt;article&gt;</c> in DOM order; on five of these seven pages that was a related-post card, a
/// comment or a header teaser, yielding 70-137 words with the recipe body missing entirely.
/// </summary>
public sealed class ReadableContentExtractorFixtureTests
{
    private static readonly ReadableContentExtractor Extractor = new();

    private static ExtractedContent Extract(string fixture, string url)
        => Extractor.Extract(HtmlFixtures.Load(fixture), url);

    [Fact]
    public void ChainBaker_Divi_Page_Without_Article_Or_Main_Reads_The_Post_Body()
    {
        // The only <article> elements are three related-post cards (~38 words each) following the post.
        var content = Extract("chainbaker-yudane-buns.html", "https://www.chainbaker.com/yudane-buns/");

        Assert.Equal("div#main-content", content.ContentSelector);
        Assert.InRange(content.WordCount, 700, 1_100);
        Assert.Contains("\nIngredients\n", content.Text);
        Assert.Contains("1. Make the yudane", content.Text);
        Assert.Equal("ChainBaker", content.SiteName);
        Assert.Equal("2022-02-02T16:00:00+00:00", content.PublishedAt);
        Assert.Equal("How to Make Super Soft Burger Buns | Yudane Method", content.Headline);
        Assert.StartsWith(content.Headline!, content.Text);
        Assert.Equal(1, CountOccurrences(content.Text, content.Headline!));
    }

    [Fact]
    public void ChainBaker_Text_Keeps_Block_Boundaries_Instead_Of_Fusing_Words()
    {
        // The detached-clone innerText path produced "Apr 23, 2025FoodI got hooked..." (date, category and excerpt fused).
        var content = Extract("chainbaker-yudane-buns.html", "https://www.chainbaker.com/yudane-buns/");

        Assert.DoesNotContain("2025Food", content.Text);
        Assert.DoesNotContain("FoodI got", content.Text);
    }

    [Fact]
    public void ChainBaker_Deli_Rye_Lists_Preferment_And_Main_Dough_Ingredients()
    {
        var content = Extract("chainbaker-deli-rye.html", "https://www.chainbaker.com/deli-rye/");

        Assert.InRange(content.WordCount, 850, 1_200);
        Assert.Contains("60g (2.1oz) wholemeal rye flour", content.Text);
        Assert.Contains("400g (14.1oz) strong white bread flour", content.Text);
        Assert.Equal("2021-08-04T15:00:00+00:00", content.PublishedAt);
    }

    [Fact]
    public void KingArthur_Picks_Main_Over_The_Recipe_Header_Card_And_Keeps_The_Ingredient_Aside()
    {
        // First <article> is a 102-word summary card; ingredients live in <aside class="recipe__ingredients">
        // (link density 0.40 because ingredient names link to products) inside <main>.
        var content = Extract("kingarthurbaking-everyday-french-loaf.html", "https://www.kingarthurbaking.com/recipes/everyday-french-loaf-recipe");

        Assert.Equal("main.main", content.ContentSelector);
        Assert.InRange(content.WordCount, 1_000, 1_300);
        Assert.Equal("Everyday French Loaf", content.Headline);
        Assert.Equal("King Arthur Baking", content.SiteName);
        Assert.Contains("- 1 3/4 cups plus 1 1/2 tablespoons (222g) King Arthur Unbleached All-Purpose Flour", content.Text);
        Assert.Contains("- 2 1/2 teaspoons (15g) table salt", content.Text);
        Assert.Contains("\nInstructions\n", content.Text);
        Assert.DoesNotContain("Chat with a baker", content.Text);
    }

    [Fact]
    public void ItDoesntTasteLikeChicken_Renders_Recipe_Card_Ingredients_As_Separate_Bullets()
    {
        // In-browser extraction returned "(see step 2)▢ 1 tablespoon light oil" with list items fused together.
        var content = Extract("itdoesnttastelikechicken-bbq-shredded-tofu.html", "https://itdoesnttastelikechicken.com/vegan-bbq-shredded-tofu-shredded-chicken/");

        Assert.Equal("main#genesis-content.content", content.ContentSelector);
        Assert.InRange(content.WordCount, 900, 1_100);
        Assert.Contains("\n- 1 tablespoon light oil, such as canola or vegetable\n", content.Text);
        Assert.Contains("\n- ½ teaspoon smoked paprika\n", content.Text);
        Assert.DoesNotContain(")▢", content.Text);
        Assert.Equal("2020-05-27T05:00:00+00:00", content.PublishedAt);
    }

    [Fact]
    public void SweetSimpleVegan_Drops_The_Yoast_Table_Of_Contents_Block()
    {
        var content = Extract("sweetsimplevegan-sticky-sesame-tofu.html", "https://sweetsimplevegan.com/sticky-sesame-tofu/");

        Assert.Equal("main.site-main", content.ContentSelector);
        Assert.InRange(content.WordCount, 1_100, 1_400);
        Assert.DoesNotContain("Table of Contents", content.Text);
        Assert.Contains("- 1 block (14 oz) extra-firm or firm tofu", content.Text);
        Assert.Contains("- 3 tablespoons low-sodium soy sauce", content.Text);
    }

    [Fact]
    public void TwoSpoons_Falls_Back_To_Body_When_The_Only_Articles_Are_Comments()
    {
        // All 14 <article> elements are comment bodies (14-34 words); the post is a plain div.
        var content = Extract("twospoons-crispy-baked-tofu.html", "https://www.twospoons.ca/30-minute-crispy-oven-baked-tofu/");

        Assert.StartsWith("body", content.ContentSelector);
        Assert.InRange(content.WordCount, 1_400, 1_700);
        Assert.Contains("- 16 oz extra-firm tofu", content.Text);
        Assert.Contains("- 2 tbsp arrowroot starch (or corn starch)", content.Text);
        Assert.DoesNotContain("Home »", content.Text);
    }

    [Fact]
    public void NytCooking_Picks_Main_Over_Recipe_Cards_And_Keeps_Bullets_Attached_To_Their_Text()
    {
        // First <article> is a 22-word "you might also like" card; list items wrap their text in <p>.
        var content = Extract("nytcooking-parmesan-crusted-potatoes.html", "https://cooking.nytimes.com/recipes/1024575-extra-crispy-parmesan-crusted-roasted-potatoes");

        Assert.Equal("main", content.ContentSelector);
        Assert.InRange(content.WordCount, 900, 1_100);
        Assert.Equal("NYT Cooking", content.SiteName);
        Assert.Contains("\n- 3 pounds russet potatoes, peeled and cut into 1 ½- to 2-inch chunks\n", content.Text);
        Assert.Contains("\n- ½ teaspoon baking soda\n", content.Text);
        Assert.DoesNotContain("- \n", content.Text);
    }

    [Theory]
    [InlineData("chainbaker-yudane-buns.html")]
    [InlineData("chainbaker-deli-rye.html")]
    [InlineData("kingarthurbaking-everyday-french-loaf.html")]
    [InlineData("itdoesnttastelikechicken-bbq-shredded-tofu.html")]
    [InlineData("sweetsimplevegan-sticky-sesame-tofu.html")]
    [InlineData("twospoons-crispy-baked-tofu.html")]
    [InlineData("nytcooking-parmesan-crusted-potatoes.html")]
    public void Every_Fixture_Yields_Most_Of_The_Body_Text_With_Clean_Whitespace(string fixture)
    {
        var content = Extract(fixture, "https://example.test/");

        Assert.True(content.WordCount >= content.BodyWordCount * ReadableContentExtractor.MinimumBodyShare,
            $"{fixture}: {content.WordCount} words extracted of {content.BodyWordCount} body words (selector {content.ContentSelector}).");
        Assert.DoesNotContain("\n\n\n", content.Text);
        Assert.DoesNotContain("  ", content.Text);
        Assert.DoesNotContain("\t", content.Text);
        Assert.Equal(content.Text, content.Text.Trim());
        Assert.Equal(ReadableContentExtractor.CountWords(content.Text), content.WordCount);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
