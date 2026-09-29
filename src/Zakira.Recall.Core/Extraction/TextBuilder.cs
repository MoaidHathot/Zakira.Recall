using System.Text;
using AngleSharp.Dom;

namespace Zakira.Recall.Core.Extraction;

internal readonly record struct TextStats(string Text, int WordCount, int LinkWordCount)
{
    /// <summary>Share of words that sit inside links; 1.0 for empty text so empty blocks never win.</summary>
    public double LinkDensity => WordCount == 0 ? 1.0 : (double)LinkWordCount / WordCount;
}

/// <summary>
/// Serializes an element to plain text the way a rendered page reads: block elements start new lines,
/// paragraphs and headings are separated by blank lines, list items get bullets, table cells get separators,
/// whitespace is collapsed, and boilerplate (see <see cref="NoiseFilter"/>) is skipped.
/// Unlike <c>textContent</c> this never fuses adjacent blocks into one word.
/// </summary>
internal sealed class TextBuilder
{
    private static readonly HashSet<string> ParagraphTags = new(StringComparer.Ordinal)
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "figure", "figcaption",
        "table", "ul", "ol", "dl", "details", "summary"
    };

    private static readonly HashSet<string> LineTags = new(StringComparer.Ordinal)
    {
        "div", "section", "article", "main", "aside", "header", "footer", "nav", "address", "fieldset", "legend",
        "li", "dd", "dt", "tr", "caption", "thead", "tbody", "tfoot", "form", "label", "menu"
    };

    private readonly NoiseFilter _noise;
    private readonly StringBuilder _text = new();

    private int _pendingBreaks;
    private bool _pendingSpace;
    private bool _atLineStart = true;

    /// <summary>Position of a list bullet that has not been followed by text yet; -1 when none.</summary>
    private int _openBulletStart = -1;

    private int _anchorDepth;
    private bool _inToken;
    private bool _tokenHasAlphanumeric;
    private bool _tokenInAnchor;
    private int _wordCount;
    private int _linkWordCount;

    private TextBuilder(NoiseFilter noise)
    {
        _noise = noise;
    }

    public static TextStats Build(IElement root, NoiseFilter noise, bool treatRootAsContent = false)
    {
        var builder = new TextBuilder(noise);
        builder.VisitElement(root, preserveWhitespace: false, isRoot: treatRootAsContent);
        builder.CloseBullet();
        builder.FinishToken();
        return new TextStats(builder._text.ToString().Trim(), builder._wordCount, builder._linkWordCount);
    }

    private void Visit(INode node, bool preserveWhitespace)
    {
        switch (node.NodeType)
        {
            case NodeType.Text:
                AppendText(node.TextContent, preserveWhitespace);
                break;
            case NodeType.Element:
                VisitElement((IElement)node, preserveWhitespace, isRoot: false);
                break;
        }
    }

    private void VisitElement(IElement element, bool preserveWhitespace, bool isRoot)
    {
        if (!isRoot && _noise.IsNoise(element))
        {
            return;
        }

        var tag = element.LocalName;
        switch (tag)
        {
            case "br":
                RequestBreak(1);
                return;
            case "hr":
                RequestBreak(2);
                return;
        }

        var isParagraph = ParagraphTags.Contains(tag);
        var isLine = !isParagraph && LineTags.Contains(tag);
        if (isParagraph)
        {
            RequestBreak(2);
        }
        else if (isLine)
        {
            RequestBreak(1);
        }

        if (tag == "li")
        {
            OpenBullet(GetListItemPrefix(element));
        }
        else if ((tag == "td" || tag == "th") && HasPreviousCell(element))
        {
            AppendRaw(" | ");
        }

        var isAnchor = tag == "a";
        if (isAnchor)
        {
            _anchorDepth++;
        }

        var preserve = preserveWhitespace || tag == "pre";
        foreach (var child in element.ChildNodes)
        {
            Visit(child, preserve);
        }

        if (isAnchor)
        {
            _anchorDepth--;
        }

        if (tag == "li")
        {
            CloseBullet();
        }

        if (isParagraph)
        {
            RequestBreak(2);
        }
        else if (isLine)
        {
            RequestBreak(1);
        }
    }

    /// <summary>
    /// Writes a bullet and keeps the item's first text on the same line even when the item wraps it in a block
    /// element (for example <c>&lt;li&gt;&lt;p&gt;…&lt;/p&gt;&lt;/li&gt;</c>).
    /// </summary>
    private void OpenBullet(string prefix)
    {
        CloseBullet();
        FinishToken();
        FlushPendingSeparators();
        _openBulletStart = _text.Length;
        _text.Append(prefix);
        _atLineStart = true;
        _pendingSpace = false;
    }

    /// <summary>Removes a bullet that never received any text (image-only or empty items).</summary>
    private void CloseBullet()
    {
        if (_openBulletStart >= 0)
        {
            _text.Length = _openBulletStart;
            _openBulletStart = -1;
            _pendingSpace = false;
            _atLineStart = _text.Length == 0 || _text[^1] == '\n';
        }
    }

    private static string GetListItemPrefix(IElement item)
    {
        var parent = item.ParentElement;
        if (parent?.LocalName == "ol")
        {
            var index = 1;
            for (var sibling = item.PreviousElementSibling; sibling is not null; sibling = sibling.PreviousElementSibling)
            {
                if (sibling.LocalName == "li")
                {
                    index++;
                }
            }

            return $"{index}. ";
        }

        return "- ";
    }

    private static bool HasPreviousCell(IElement cell)
    {
        for (var sibling = cell.PreviousElementSibling; sibling is not null; sibling = sibling.PreviousElementSibling)
        {
            if (sibling.LocalName is "td" or "th")
            {
                return true;
            }
        }

        return false;
    }

    private void RequestBreak(int count)
    {
        if (_text.Length == 0 || _openBulletStart >= 0)
        {
            // Nothing to separate yet, or a bullet is waiting for its first text.
            return;
        }

        FinishToken();
        _pendingBreaks = Math.Max(_pendingBreaks, count);
    }

    private void FlushPendingSeparators()
    {
        if (_pendingBreaks > 0)
        {
            // A truncated empty bullet may have left line breaks at the end of the buffer; never stack more than two.
            var missing = Math.Min(_pendingBreaks, 2) - TrailingNewlineCount();
            if (missing > 0)
            {
                _text.Append('\n', missing);
            }

            _pendingBreaks = 0;
            _pendingSpace = false;
            _atLineStart = true;
        }
        else if (_pendingSpace && !_atLineStart)
        {
            _text.Append(' ');
            _pendingSpace = false;
        }
        else
        {
            _pendingSpace = false;
        }
    }

    private int TrailingNewlineCount()
    {
        var count = 0;
        for (var index = _text.Length - 1; index >= 0 && count < 2 && _text[index] == '\n'; index--)
        {
            count++;
        }

        return count;
    }

    /// <summary>Appends structural text (cell separators) without counting it as words.</summary>
    private void AppendRaw(string value)
    {
        FinishToken();
        FlushPendingSeparators();
        _openBulletStart = -1;
        _text.Append(value);
        _atLineStart = false;
        _pendingSpace = false;
    }

    private void AppendText(string value, bool preserveWhitespace)
    {
        if (value.Length == 0)
        {
            return;
        }

        if (preserveWhitespace)
        {
            FinishToken();
            FlushPendingSeparators();
            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch))
                {
                    FinishToken();
                }
                else
                {
                    StartOrExtendToken(ch);
                }

                _text.Append(ch);
            }

            _atLineStart = value[^1] == '\n';
            return;
        }

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                FinishToken();
                _pendingSpace = true;
                continue;
            }

            FlushPendingSeparators();
            StartOrExtendToken(ch);
            _text.Append(ch);
            _atLineStart = false;
        }
    }

    private void StartOrExtendToken(char ch)
    {
        _openBulletStart = -1;
        if (!_inToken)
        {
            _inToken = true;
            _tokenInAnchor = _anchorDepth > 0;
            _tokenHasAlphanumeric = false;
        }

        if (IsWordCharacter(ch))
        {
            _tokenHasAlphanumeric = true;
        }
    }

    /// <summary>Letters, digits and other numeric characters such as vulgar fractions (½) make a token a word.</summary>
    internal static bool IsWordCharacter(char ch)
        => char.IsLetterOrDigit(ch) || char.IsNumber(ch);

    private void FinishToken()
    {
        if (!_inToken)
        {
            return;
        }

        if (_tokenHasAlphanumeric)
        {
            _wordCount++;
            if (_tokenInAnchor)
            {
                _linkWordCount++;
            }
        }

        _inToken = false;
        _tokenHasAlphanumeric = false;
    }
}
