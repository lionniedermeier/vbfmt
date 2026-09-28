using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language;

/// <summary>
/// Turns trivia into document parts. Roslyn keeps every comment in the tree, so -- unlike a
/// formatter over an AST that drops them -- there is no attachment pass: a comment is printed by
/// whoever prints the token it hangs on, and that happens exactly once.
/// </summary>
internal static class TriviaPrinter
{
    /// <summary>
    /// The comments, documentation comments and directives above <paramref name="token"/>, each
    /// closed by the line break that separates it from what follows. Blank lines <em>before</em> the
    /// first of them belong to the enclosing list and are reported by
    /// <see cref="BlankLinesBefore(SyntaxToken)"/> instead.
    /// </summary>
    public static Doc Leading(SyntaxToken token, FormatContext context) =>
        Leading(token.LeadingTrivia, token.LeadingTrivia.Count, context);

    public static Doc Leading(
        SyntaxTriviaList trivia,
        int count,
        FormatContext context,
        int blankAfter = -1
    )
    {
        var (content, closingBreak) = Split(trivia, count, blankAfter);
        return Doc.Concat(content, closingBreak);
    }

    public static (Doc Content, Doc Break) Dangling(SyntaxToken token) =>
        Split(token.LeadingTrivia, token.LeadingTrivia.Count, -1);

    private static (Doc Content, Doc Break) Split(
        SyntaxTriviaList trivia,
        int count,
        int blankAfter
    )
    {
        // The overwhelmingly common case: indentation and blank lines only, nothing to print above
        // the token. Answered without allocating a builder.
        if (!HasPrintableTrivia(trivia, count))
        {
            return (Doc.Nothing, Doc.Nothing);
        }

        var parts = ImmutableArray.CreateBuilder<Doc>();
        var blankLines = 0;
        var written = false;
        var lastContentIndex = -1;

        for (var i = 0; i < count; i++)
        {
            var current = trivia[i];

            if (current.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (IsBlankLine(trivia, i))
                {
                    blankLines++;
                }

                continue;
            }

            if (Content(current) is not { } content)
            {
                continue;
            }

            if (written)
            {
                var forced = lastContentIndex < blankAfter && i >= blankAfter;
                parts.Add(blankLines > 0 || forced ? Doc.EmptyLine : Doc.HardLine);
            }

            parts.Add(content);
            blankLines = 0;
            written = true;
            lastContentIndex = i;
        }

        var closingBreak =
            blankLines > 0 || lastContentIndex < blankAfter ? Doc.EmptyLine : Doc.HardLine;

        return (Doc.Concat(parts.DrainToImmutable()), closingBreak);
    }

    /// <summary>
    /// A comment behind <paramref name="token"/> on the same line. It is parked as a line suffix so
    /// that it stays on that line even when a break is inserted before it. While code still follows
    /// the comment inside the same statement it also forces the enclosing group to break, so that
    /// following code cannot end up on the same line as the comment and be lost behind it; behind the
    /// last token of a statement nothing follows, and the break the statement list already puts after
    /// it is enough.
    /// </summary>
    public static Doc Trailing(SyntaxToken token, FormatContext context)
    {
        if (!HasPrintableTrivia(token.TrailingTrivia))
        {
            return Doc.Nothing;
        }

        var parts = ImmutableArray.CreateBuilder<Doc>();
        bool? expands = null;

        foreach (var trivia in token.TrailingTrivia)
        {
            if (Content(trivia) is not { } content)
            {
                continue;
            }

            parts.Add(Doc.LineSuffix(Doc.Concat(Doc.Space, content)));

            if (expands ??= HasCodeBehind(token))
            {
                parts.Add(Doc.ExpandParent);
            }
        }

        return Doc.Concat(parts.DrainToImmutable());
    }

    /// <summary>
    /// Whether a token still follows <paramref name="token"/> inside the statement it belongs to. When
    /// none does, a comment behind it is the last thing on the line and needs no forced break.
    /// </summary>
    private static bool HasCodeBehind(SyntaxToken token) =>
        token.Parent?.FirstAncestorOrSelf<StatementSyntax>() is not { } statement
        || statement.GetLastToken() != token;

    /// <summary>Whether any trivium would print something -- a comment, a directive, disabled text.</summary>
    private static bool HasPrintableTrivia(SyntaxTriviaList trivia) =>
        HasPrintableTrivia(trivia, trivia.Count);

    private static bool HasPrintableTrivia(SyntaxTriviaList trivia, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var trivium = trivia[i];

            if (
                trivium.IsDirective
                || trivium.IsKind(SyntaxKind.CommentTrivia)
                || trivium.IsKind(SyntaxKind.DocumentationCommentTrivia)
                || trivium.IsKind(SyntaxKind.DisabledTextTrivia)
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How many blank lines the author left above <paramref name="token"/>.</summary>
    public static int BlankLinesBefore(SyntaxToken token)
    {
        var trivia = token.LeadingTrivia;
        var blankLines = 0;

        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (IsBlankLine(trivia, i))
                {
                    blankLines++;
                }

                continue;
            }

            // Whitespace is indentation; anything else already belongs to the leading comments.
            if (!trivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                break;
            }
        }

        return blankLines;
    }

    /// <summary>
    /// An end of line that no content preceded on the same line. A directive and a run of disabled
    /// text carry their own line ending, so an end of line behind one starts an empty line just as
    /// an end of line behind an end of line does.
    /// </summary>
    private static bool IsBlankLine(SyntaxTriviaList trivia, int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = trivia[index - 1];

        // A directive and a run of disabled text each carry their own trailing line ending.
        return previous.IsKind(SyntaxKind.WhitespaceTrivia)
            || previous.IsKind(SyntaxKind.EndOfLineTrivia)
            || previous.IsDirective
            || previous.IsKind(SyntaxKind.DisabledTextTrivia);
    }

    /// <summary>
    /// What a trivium contributes, or <c>null</c> when the printer owns it: whitespace and line
    /// breaks are indentation, a colon separates statements the block rule puts on their own lines
    /// anyway, and an underscore continuation is re-decided from scratch.
    /// </summary>
    private static Doc? Content(SyntaxTrivia trivia)
    {
        if (trivia.IsKind(SyntaxKind.CommentTrivia))
        {
            return Doc.Text(trivia.ToString().TrimEnd());
        }

        if (trivia.IsKind(SyntaxKind.DocumentationCommentTrivia))
        {
            return DocumentationComment(trivia);
        }

        // A directive is line-bound, is reproduced as it stands, and sits at the left margin the
        // way every VB tool writes it -- so it is emitted raw rather than at the current indent.
        if (trivia.IsDirective)
        {
            return VerbatimFormatter.Raw(trivia.ToString().Trim());
        }

        if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
        {
            return VerbatimFormatter.Raw(trivia.ToString().TrimEnd('\r', '\n'));
        }

        return null;
    }

    /// <summary>
    /// A <c>'''</c> block, line by line and never reflowed. Only the indentation is dropped -- the
    /// spacing behind the exterior marker is the author's.
    /// </summary>
    private static Doc DocumentationComment(SyntaxTrivia trivia)
    {
        var lines = VerbatimFormatter
            .SplitLines(trivia.ToFullString())
            .Select(l => l.TrimStart().TrimEnd())
            .Where(l => l.Length > 0)
            .Select(Doc.Text);

        return Doc.Join(Doc.HardLine, lines);
    }
}
