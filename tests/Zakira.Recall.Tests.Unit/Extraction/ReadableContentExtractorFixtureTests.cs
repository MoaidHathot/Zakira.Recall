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
        Assert.Equal(DateTimeOffset.Parse("2022-02-02T16:00:00+00:00"), content.PublishedAt);
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
        Assert.Equal(DateTimeOffset.Parse("2021-08-04T15:00:00+00:00"), content.PublishedAt);
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
        Assert.Equal(DateTimeOffset.Parse("2020-05-27T05:00:00+00:00"), content.PublishedAt);
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

    [Fact]
    public void KingArthur_Exposes_The_Recipe_Json_Ld_And_Backfills_Date_And_Image_From_It()
    {
        // No article:published_time meta; the Recipe declares datePublished as "July 8, 2024 at 2:48pm".
        var content = Extract("kingarthurbaking-everyday-french-loaf.html", "https://www.kingarthurbaking.com/recipes/everyday-french-loaf-recipe");

        var structured = content.StructuredData;
        Assert.NotNull(structured);
        Assert.Equal(["Recipe"], structured!.Types);
        Assert.Equal("Recipe", structured.MainEntityType);
        var recipe = structured.MainEntity!.Value;
        Assert.Equal("Everyday French Loaf", recipe.GetProperty("name").GetString());
        Assert.Equal(7, recipe.GetProperty("recipeIngredient").GetArrayLength());
        Assert.Equal(17, recipe.GetProperty("recipeInstructions").GetArrayLength());
        Assert.Equal("PT20M", recipe.GetProperty("prepTime").GetString());
        Assert.Equal("PT20H0M", recipe.GetProperty("totalTime").GetString());
        Assert.Equal(new DateTimeOffset(2024, 7, 8, 14, 48, 0, TimeSpan.Zero), content.PublishedAt);
        Assert.Equal("https://www.kingarthurbaking.com/sites/default/files/2024-07/Everyday-French-Loaf_1366.jpg", content.MainImage);
    }

    [Fact]
    public void NytCooking_Recipe_Json_Ld_Is_Returned_Without_Its_Review_Payload()
    {
        var content = Extract("nytcooking-parmesan-crusted-potatoes.html", "https://cooking.nytimes.com/recipes/1024575");

        var structured = content.StructuredData!;
        Assert.Equal("Recipe", structured.MainEntityType);
        Assert.Contains("WebPage", structured.Types);
        var recipe = structured.MainEntity!.Value;
        Assert.Equal(10, recipe.GetProperty("recipeIngredient").GetArrayLength());
        Assert.False(recipe.TryGetProperty("review", out _));
        Assert.InRange(recipe.GetRawText().Length, 5_000, StructuredDataExtractor.MaxMainEntityJsonLength);
        Assert.Equal(new DateTimeOffset(2019, 11, 20, 0, 0, 0, TimeSpan.Zero), content.PublishedAt);
        Assert.StartsWith("https://static01.nyt.com/images/", content.MainImage);
    }

    [Fact]
    public void ChainBaker_Declares_Only_An_Article_So_Text_Extraction_Is_The_Only_Source_Of_The_Recipe()
    {
        var content = Extract("chainbaker-yudane-buns.html", "https://www.chainbaker.com/yudane-buns/");

        Assert.Equal("Article", content.StructuredData!.MainEntityType);
        Assert.DoesNotContain("Recipe", content.StructuredData.Types);
        Assert.Equal("https://www.chainbaker.com/wp-content/uploads/2022/01/IMG_2198.jpg", content.MainImage);
    }

    [Theory]
    [InlineData("itdoesnttastelikechicken-bbq-shredded-tofu.html", 8, 5)]
    [InlineData("sweetsimplevegan-sticky-sesame-tofu.html", 15, 7)]
    [InlineData("twospoons-crispy-baked-tofu.html", 7, 2)]
    public void WordPress_Recipe_Plugins_Yield_A_Recipe_Main_Entity(string fixture, int ingredients, int instructions)
    {
        var content = Extract(fixture, "https://example.test/");

        var recipe = content.StructuredData!.MainEntity!.Value;
        Assert.Equal("Recipe", content.StructuredData.MainEntityType);
        Assert.Equal(ingredients, recipe.GetProperty("recipeIngredient").GetArrayLength());
        Assert.Equal(instructions, recipe.GetProperty("recipeInstructions").GetArrayLength());
        Assert.NotNull(content.MainImage);
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
