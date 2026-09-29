using System.Runtime.CompilerServices;

namespace Zakira.Recall.Tests.Unit.Extraction;

/// <summary>
/// Real-world pages captured on 2026-09-29 (server HTML, CRLF normalized to LF). Each one documents an
/// extraction failure mode observed in production, see <see cref="ReadableContentExtractorFixtureTests"/>.
/// </summary>
internal static class HtmlFixtures
{
    public static string Directory { get; } = Path.Combine(GetTestsRoot(), "Fixtures", "Html");

    public static string Load(string fileName)
        => File.ReadAllText(Path.Combine(Directory, fileName));

    private static string GetTestsRoot([CallerFilePath] string filePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, ".."));
}
