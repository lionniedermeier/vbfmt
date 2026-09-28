using Microsoft.CodeAnalysis;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language.Statements;

/// <summary>
/// The shape every VB block has: a header line, a body one level deeper, and a closing statement
/// back at the header's indent. The node types do not share a base that exposes those three parts,
/// so each block rule reads its own properties and hands them here.
/// </summary>
internal static class BlockRule
{
    /// <summary>header, indented body, footer.</summary>
    public static Doc Format<T>(
        Doc header,
        SyntaxList<T> body,
        SyntaxNode? footer,
        VbDocVisitor visitor,
        FormatContext context
    )
        where T : SyntaxNode =>
        Doc.Concat(header, Body(body, visitor, context), Footer(footer, visitor, context));

    /// <summary>The body of a block, one level deeper than its header.</summary>
    public static Doc Body<T>(SyntaxList<T> body, VbDocVisitor visitor, FormatContext context)
        where T : SyntaxNode => Doc.Indent(StatementListRule.Format(body, visitor, context));

    public static Doc Footer(
        SyntaxNode? footer,
        VbDocVisitor visitor,
        FormatContext context,
        Doc? separator = null
    )
    {
        if (footer is null)
        {
            return Doc.Nothing;
        }

        separator ??= context.Separator(footer);
        var first = footer.GetFirstToken();

        if (context.IsIgnored(footer))
        {
            return Doc.Concat(separator, visitor.Format(footer));
        }

        var (content, closingBreak) = TriviaPrinter.Dangling(first);

        if (Doc.IsNothing(content))
        {
            return Doc.Concat(separator, visitor.Format(footer));
        }

        var previous = context.Hoist(first);
        var body = visitor.Format(footer);
        context.Restore(previous);

        return Doc.Concat(Doc.Indent(separator, content), closingBreak, body);
    }
}
