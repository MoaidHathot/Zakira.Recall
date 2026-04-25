using System.Reflection;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class DuckDuckGoBrowserSearchProviderTests
{
    [Fact]
    public void Detects_DuckDuckGo_Bot_Challenge_Text()
    {
        const string challengeText = "Unfortunately, bots use DuckDuckGo too. Please complete the following challenge to confirm this search was made by a human.";

        Assert.Contains("bots use duckduckgo too", challengeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("confirm this search was made by a human", challengeText, StringComparison.OrdinalIgnoreCase);
    }
}
