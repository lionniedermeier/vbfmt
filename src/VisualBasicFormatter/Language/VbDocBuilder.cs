using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using VisualBasicFormatter.Language.Statements;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language;

/// <summary>How a broken list arranges its elements below the opening bracket.</summary>
internal enum ListLayout
{
    /// <summary>
    /// One element per line. What a list of named things wants: an added or removed element is a
    /// one-line diff, and nothing above it reflows.
    /// </summary>
    OnePerLine,

    /// <summary>
    /// All elements on a single indented line first, falling back to
    /// <see cref="OnePerLine"/> only when that line does not fit either. What a signature or a call
    /// wants: three short arguments read better on one line than stacked.
    /// </summary>
    Packed,
}

/// <summary>The two shapes the wrapping rules share.</summary>
internal static class VbDocBuilder
{
    /// <summary>
    /// A bracketed list. When it does not fit flat it breaks after the opening bracket, indents its
    /// elements one level, and puts the closing bracket on a line of its own.
    /// </summary>
    /// <param name="open">Opening paren, brace or angle bracket.</param>
    /// <param name="prefix">What stands between the opener and the first element, e.g. <c>Of</c>.</param>
    /// <param name="list">The elements, with their separators.</param>
    /// <param name="close">Closing bracket.</param>
    /// <param name="layout">How the elements are arranged once the list breaks.</param>
    /// <param name="visitor">Formats the elements.</param>
    /// <param name="context">Options, tokens and the break legality check.</param>
    public static Doc List<T>(
        SyntaxToken open,
        Doc prefix,
        SeparatedSyntaxList<T> list,
        SyntaxToken close,
        ListLayout layout,
        VbDocVisitor visitor,
        FormatContext context
    )
        where T : SyntaxNode
    {
        var (elements, separators) = ToArrays(list, visitor);
        var hasBlock = false;

        foreach (var element in list)
        {
            if (TrailingExpansion.IsBlock(element))
            {
                hasBlock = true;
                break;
            }
        }

        return List(
            open,
            prefix,
            elements,
            separators,
            close,
            list.Count > 0 && TrailingExpansion.IsExpandable(list[^1]),
            hasBlock,
            layout,
            context
        );
    }

    public static Doc Run<T>(
        SeparatedSyntaxList<T> list,
        VbDocVisitor visitor,
        FormatContext context
    )
        where T : SyntaxNode
    {
        var (elements, separators) = ToArrays(list, visitor);

        return Run(Items(Doc.Nothing, elements, separators, context));
    }

    public static Doc Bracketed<T>(
        SyntaxToken open,
        SeparatedSyntaxList<T> list,
        SyntaxToken close,
        ListLayout layout,
        VbDocVisitor visitor,
        FormatContext context
    )
        where T : SyntaxNode
    {
        var (elements, separators) = ToArrays(list, visitor);

        if (elements.IsEmpty)
        {
            return Doc.Concat(context.Token(open), context.Token(close));
        }

        return Bracketed(
            open,
            Items(Doc.Nothing, elements, separators, context),
            close,
            layout,
            context
        );
    }

    private static (
        ImmutableArray<Doc> Elements,
        ImmutableArray<SyntaxToken> Separators
    ) ToArrays<T>(SeparatedSyntaxList<T> list, VbDocVisitor visitor)
        where T : SyntaxNode
    {
        var count = list.Count;
        var elements = count == 0 ? [] : new Doc[count];

        for (var i = 0; i < count; i++)
        {
            elements[i] = visitor.Format(list[i]);
        }

        var separatorCount = list.SeparatorCount;
        var separators = separatorCount == 0 ? [] : new SyntaxToken[separatorCount];

        for (var i = 0; i < separatorCount; i++)
        {
            separators[i] = list.GetSeparator(i);
        }

        return (
            ImmutableCollectionsMarshal.AsImmutableArray(elements),
            ImmutableCollectionsMarshal.AsImmutableArray(separators)
        );
    }

    /// <inheritdoc cref="List{T}(SyntaxToken, Doc, SeparatedSyntaxList{T}, SyntaxToken, ListLayout, VbDocVisitor, FormatContext)"/>
    public static Doc List<T>(
        SyntaxToken open,
        SeparatedSyntaxList<T> list,
        SyntaxToken close,
        ListLayout layout,
        VbDocVisitor visitor,
        FormatContext context
    )
        where T : SyntaxNode => List(open, Doc.Nothing, list, close, layout, visitor, context);

    /// <summary>
    /// The same, for a construct whose elements are not a <see cref="SeparatedSyntaxList{T}"/> --
    /// the ternary <c>If</c>, whose commas are children of the expression itself.
    /// </summary>
    /// <param name="open">Opening paren, brace or angle bracket.</param>
    /// <param name="prefix">What stands between the opener and the first element.</param>
    /// <param name="elements">One doc per element, already formatted.</param>
    /// <param name="separators">The commas between them, one fewer than there are elements.</param>
    /// <param name="close">Closing bracket.</param>
    /// <param name="expandLast">
    /// Whether the last element may be expanded on its own before the separators here are broken.
    /// </param>
    /// <param name="hasBlock">
    /// Whether any element is a multi-line lambda, per <see cref="TrailingExpansion.IsBlock"/>. Such
    /// an element brings its own indented body, so it can never share a line with a neighbour --
    /// which rules <see cref="ListLayout.Packed"/> out however this list is otherwise laid out.
    /// </param>
    /// <param name="layout">How the elements are arranged once the list breaks.</param>
    /// <param name="context">Options, tokens and the break legality check.</param>
    public static Doc List(
        SyntaxToken open,
        Doc prefix,
        ImmutableArray<Doc> elements,
        ImmutableArray<SyntaxToken> separators,
        SyntaxToken close,
        bool expandLast,
        bool hasBlock,
        ListLayout layout,
        FormatContext context
    )
    {
        var items = Items(prefix, elements, separators, context);

        // A block's body would start one level further in than wherever the bracket happens to end,
        // leaving it that much less width. Below the bracket it starts at a column the statement's
        // own indent decides, which is what gives the body back the width it needs -- and packing a
        // block onto a shared line is exactly what would take it away again.
        if (hasBlock)
        {
            return Doc.Group(Bracketed(open, items, close, ListLayout.OnePerLine, context));
        }

        // Without a separator the only break on offer is the one behind the bracket, and that just
        // moves the problem to the next line. Leaving it out is what lets a lone argument keep its
        // call on one line and break inside itself instead.
        if (separators.IsEmpty && (elements.IsEmpty || expandLast || layout != ListLayout.Packed))
        {
            return Doc.Concat(
                context.Token(open),
                prefix,
                Doc.Concat(elements),
                context.Token(close)
            );
        }

        var content = Bracketed(open, items, close, layout, context);
        var group = Doc.Group(content);

        // Content that breaks anyway has no choice left to make.
        if (group.Expands || !expandLast || Doc.ForceBreak(elements[^1]) is not { } expanded)
        {
            return group;
        }

        // Least to most expanded. The printer takes the first whose line still fits, so a trailing
        // braced or lambda body is broken open before the separators in front of it are. The first
        // two layouts are deliberately not groups: a group above the expanded element would inherit
        // its break and break every separator here as well, which is the very layout they are there
        // to avoid.
        //
        // Note that hugging outranks packing: the middle rung keeps the head of the list on the
        // current line, while the last rung has already given that line up. Settled against
        // LambdaArgument.vb and NestedTernary.vb, which are what change if it is turned around.
        //
        // The hug rung is built at OnePerLine whatever this list asked for. A packed body is a group,
        // and the expanded element inside it breaks unconditionally -- so that group would expand
        // too and break every separator here, which is the one thing the hug exists to avoid.
        return Doc.ConditionalGroup(
            content,
            Bracketed(
                open,
                Items(prefix, elements.SetItem(elements.Length - 1, expanded), separators, context),
                close,
                ListLayout.OnePerLine,
                context
            ),
            Doc.Group(content, shouldBreak: true)
        );
    }

    /// <summary>
    /// A run whose head stays on the current line and whose continuation lines hang below it: a row
    /// of equally ranked operators.
    /// </summary>
    /// <param name="items">Content and separator in turn, starting and ending with content.</param>
    /// <param name="indent">
    /// Whether this run contributes its own indent. A run nested inside another run of the same kind
    /// -- a higher-precedence operator chain feeding into a lower-precedence one -- passes
    /// <see langword="false"/>: the outer run already owns the indent, and <see cref="Doc.Indent(Doc)"/>
    /// composes additively, so a second one would push its continuation line one level too deep.
    /// It still gets its own <see cref="Doc.Group(Doc, bool)"/>, so it keeps its independent
    /// flat-or-broken decision.
    /// </param>
    public static Doc Run(ImmutableArray<Doc> items, bool indent = true) =>
        indent ? Doc.Group(Doc.Indent(Doc.Concat(items))) : Doc.Group(Doc.Concat(items));

    /// <summary>
    /// A run that cannot be taken apart into items: an invocation chain, whose links are nested
    /// rather than laid out side by side.
    /// </summary>
    public static Doc Run(Doc content) => Doc.Group(Doc.Indent(content));

    /// <summary>
    /// Content and separator in turn: every element carries the comma behind it, and between two of
    /// them stands the break that comma permits.
    /// </summary>
    private static ImmutableArray<Doc> Items(
        Doc prefix,
        ImmutableArray<Doc> elements,
        ImmutableArray<SyntaxToken> separators,
        FormatContext context
    )
    {
        using var items = new DocListBuilder(2 * elements.Length - 1);

        for (var i = 0; i < elements.Length; i++)
        {
            var element = i == 0 ? Doc.Concat(prefix, elements[i]) : elements[i];

            if (i >= separators.Length)
            {
                items.Add(element);
                continue;
            }

            items.Add(Doc.Concat(element, context.Token(separators[i])));
            items.Add(context.BreakAfter(separators[i]));
        }

        return items.ToImmutable();
    }

    /// <summary>The layout, still without the group that decides between flat and broken.</summary>
    /// <remarks>
    /// <see cref="ListLayout.Packed"/> needs no extra layout and no second
    /// <see cref="Doc.ConditionalGroup(Doc[])"/>: a group of its own around the items is re-measured
    /// from the column it then starts at, so it stays flat when the elements fit on one indented line
    /// and breaks every separator when they do not. Without that group the items inherit the outer
    /// group's broken mode and go one per line, which is the other layout.
    /// <para>
    /// The break in front of <paramref name="close"/> is what puts the closing bracket on a line of
    /// its own. <see cref="ContinuationPoints.IsImplicitBefore"/> grants it for <c>)</c> and <c>}</c>
    /// and refuses it for the <c>&gt;</c> of an attribute list, where it renders as nothing and the
    /// bracket stays glued -- which is the intended shape there.
    /// </para>
    /// </remarks>
    private static Doc Bracketed(
        SyntaxToken open,
        ImmutableArray<Doc> items,
        SyntaxToken close,
        ListLayout layout,
        FormatContext context
    )
    {
        var body = Doc.Concat(items);
        var closeBreak = context.SoftBreakBefore(close);

        return Doc.Concat(
            context.Token(open),
            Doc.Indent(
                context.SoftBreakAfter(open),
                layout == ListLayout.Packed ? Doc.Group(body) : body
            ),
            BlockHeader.ClosesHeader(close) ? Doc.DedentToRoot(closeBreak) : closeBreak,
            context.Token(close)
        );
    }
}
