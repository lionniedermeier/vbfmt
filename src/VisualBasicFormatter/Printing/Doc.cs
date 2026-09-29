using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace VisualBasicFormatter.Printing;

/// <summary>
/// A node of the intermediate representation the formatting rules build and
/// <see cref="DocPrinter"/> renders. Every rule returns a <see cref="Doc"/>; no rule ever writes
/// text or decides a line break itself.
/// </summary>
internal abstract class Doc
{
    /// <summary>Nothing at all.</summary>
    public static Doc Nothing => DocNothing.Instance;

    /// <summary>A single space that is never turned into a line break.</summary>
    public static Doc Space { get; } = new DocText(" ");

    /// <summary>Nothing when flat, a line break when broken.</summary>
    public static Doc SoftLine => DocLine.Soft;

    /// <summary>A space when flat, a line break when broken.</summary>
    public static Doc Line => DocLine.SpaceOrBreak;

    /// <summary>Always a line break.</summary>
    public static Doc HardLine => DocLine.Hard;

    /// <summary>Always a line break preceded by a blank line.</summary>
    public static Doc EmptyLine => DocLine.Empty;

    /// <summary>Forces every enclosing group to break without emitting anything.</summary>
    public static Doc ExpandParent => DocExpandParent.Instance;

    /// <summary>True when this doc contains an unconditional break, so no enclosing group can stay flat.</summary>
    internal abstract bool Expands { get; }

    /// <summary>Whether <paramref name="doc"/> emits nothing at all.</summary>
    public static bool IsNothing(Doc doc) => doc is DocNothing;

    /// <summary>Literal output. Must not contain a line break.</summary>
    public static Doc Text(string text) => text.Length == 0 ? Nothing : new DocText(text);

    /// <summary>A sequence. Empty parts are dropped, so rules can return <see cref="Nothing"/> freely.</summary>
    /// <remarks>
    /// The <see langword="params"/> array is freshly allocated by the compiler at every call site and
    /// never aliased, so it is adopted as the backing store rather than copied.
    /// </remarks>
    public static Doc Concat(params Doc[] parts) =>
        Concat(ImmutableCollectionsMarshal.AsImmutableArray(parts));

    /// <summary>Two parts, without allocating an array when one of them is <see cref="Nothing"/>.</summary>
    public static Doc Concat(Doc first, Doc second)
    {
        if (first is DocNothing)
        {
            return second;
        }

        if (second is DocNothing)
        {
            return first;
        }

        return new DocConcat(ImmutableArray.Create(first, second));
    }

    /// <inheritdoc cref="Concat(Doc, Doc)"/>
    public static Doc Concat(Doc first, Doc second, Doc third)
    {
        var dropped =
            (first is DocNothing ? 1 : 0)
            + (second is DocNothing ? 1 : 0)
            + (third is DocNothing ? 1 : 0);

        if (dropped == 0)
        {
            return new DocConcat(ImmutableArray.Create(first, second, third));
        }

        return Concat(ImmutableCollectionsMarshal.AsImmutableArray<Doc>([first, second, third]));
    }

    public static Doc Concat(Doc first, Doc second, Doc third, Doc fourth)
    {
        var dropped =
            (first is DocNothing ? 1 : 0)
            + (second is DocNothing ? 1 : 0)
            + (third is DocNothing ? 1 : 0)
            + (fourth is DocNothing ? 1 : 0);

        if (dropped == 0)
        {
            return new DocConcat(ImmutableArray.Create(first, second, third, fourth));
        }

        return Concat(
            ImmutableCollectionsMarshal.AsImmutableArray<Doc>([first, second, third, fourth])
        );
    }

    /// <inheritdoc cref="Concat(Doc[])"/>
    public static Doc Concat(ImmutableArray<Doc> parts)
    {
        var dropped = 0;
        foreach (var part in parts)
        {
            if (part is DocNothing)
            {
                dropped++;
            }
        }

        // Nothing to drop: hand the array straight through.
        if (dropped == 0)
        {
            return parts.Length switch
            {
                0 => Nothing,
                1 => parts[0],
                _ => new DocConcat(parts),
            };
        }

        var kept = parts.Length - dropped;
        if (kept == 0)
        {
            return Nothing;
        }

        var result = new Doc[kept];
        var index = 0;
        foreach (var part in parts)
        {
            if (part is not DocNothing)
            {
                result[index++] = part;
            }
        }

        return kept == 1
            ? result[0]
            : new DocConcat(ImmutableCollectionsMarshal.AsImmutableArray(result));
    }

    /// <summary>The flat-or-broken decision unit.</summary>
    public static Doc Group(Doc content, bool shouldBreak = false) =>
        content is DocNothing ? Nothing : new DocGroup(content, shouldBreak);

    /// <inheritdoc cref="Group(Doc, bool)"/>
    public static Doc Group(params Doc[] parts) => Group(Concat(parts));

    /// <summary>
    /// A choice between prepared layouts of the same content, most compact first. The printer takes
    /// the first whose first line fits and falls back to the last.
    /// </summary>
    public static Doc ConditionalGroup(params Doc[] states) =>
        ConditionalGroup(ImmutableCollectionsMarshal.AsImmutableArray(states));

    /// <inheritdoc cref="ConditionalGroup(Doc[])"/>
    public static Doc ConditionalGroup(ImmutableArray<Doc> states) =>
        states.Length switch
        {
            0 => Nothing,
            1 => states[0],
            _ => new DocConditionalGroup(states),
        };

    /// <summary>Content one indent level deeper.</summary>
    public static Doc Indent(Doc content) =>
        content is DocNothing ? Nothing : new DocIndent(content);

    public static Doc Indent(Doc first, Doc second) => Indent(Concat(first, second));

    /// <inheritdoc cref="Indent(Doc)"/>
    public static Doc Indent(params Doc[] parts) => Indent(Concat(parts));

    /// <summary>Content whose continuation lines start at the column this was opened at.</summary>
    public static Doc Align(Doc content) => content is DocNothing ? Nothing : new DocAlign(content);

    /// <inheritdoc cref="Align(Doc)"/>
    public static Doc Align(params Doc[] parts) => Align(Concat(parts));

    public static Doc DedentToRoot(Doc content) =>
        content is DocNothing ? Nothing : new DocDedentToRoot(content);

    public static Doc WholeFit(Doc content) =>
        content is DocNothing ? Nothing : new DocWholeFit(content);

    public static Doc RootChoice(Doc preferred, Doc fallback) =>
        new DocRootChoice(preferred, fallback);

    /// <summary>Emits <paramref name="whenBroken"/> or <paramref name="whenFlat"/> per the group's mode.</summary>
    public static Doc Conditional(Doc whenBroken, Doc whenFlat) =>
        whenBroken is DocNothing && whenFlat is DocNothing
            ? Nothing
            : new DocConditional(whenBroken, whenFlat);

    /// <summary>Content held back until the current line ends.</summary>
    public static Doc LineSuffix(Doc content) =>
        content is DocNothing ? Nothing : new DocLineSuffix(content);

    /// <summary>Source reproduced as it stands, with no break opportunity inside it.</summary>
    public static Doc Verbatim(ImmutableArray<string> lines, VerbatimMode mode) =>
        lines.Length switch
        {
            0 => Nothing,
            1 when mode != VerbatimMode.Raw => Text(lines[0]),
            _ => new DocVerbatim(lines, mode),
        };

    /// <summary>Greedy filling. <paramref name="parts"/> alternate content and separator.</summary>
    /// <param name="parts">Content and separator in turn, starting and ending with content.</param>
    /// <param name="strict">Measure the parts flat rather than in the enclosing mode.</param>
    public static Doc Fill(ImmutableArray<Doc> parts, bool strict = false) =>
        parts.Length switch
        {
            0 => Nothing,
            1 => parts[0],
            _ => new DocFill(parts, strict),
        };

    /// <summary>
    /// The same doc with its outermost layout decision already taken in favour of breaking, or
    /// <c>null</c> when it holds no decision to take.
    /// </summary>
    /// <remarks>
    /// Wrapping the doc in a further broken group would achieve nothing -- the group inside it would
    /// simply be measured again and stay flat. The decision has to be rewritten where it sits.
    /// </remarks>
    public static Doc? ForceBreak(Doc doc)
    {
        switch (doc)
        {
            case DocGroup { ShouldBreak: true }:
                return doc;

            case DocGroup group:
                return new DocGroup(group.Content, shouldBreak: true);

            // Its own most expanded layout is what this one would fall back to anyway.
            case DocConditionalGroup choice:
                return choice.States[^1];

            case DocIndent indent:
                return ForceBreak(indent.Content) is { } indented ? new DocIndent(indented) : null;

            case DocAlign align:
                return ForceBreak(align.Content) is { } aligned ? new DocAlign(aligned) : null;

            // The last part that holds a decision, so that a trailing call breaks rather than the
            // keyword in front of it.
            case DocConcat concat:
                for (var i = concat.Parts.Length - 1; i >= 0; i--)
                {
                    if (ForceBreak(concat.Parts[i]) is not { } part)
                    {
                        continue;
                    }

                    return new DocConcat(concat.Parts.SetItem(i, part));
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>Joins <paramref name="parts"/> with <paramref name="separator"/>.</summary>
    public static Doc Join(Doc separator, IEnumerable<Doc> parts)
    {
        var builder = ImmutableArray.CreateBuilder<Doc>();

        foreach (var part in parts)
        {
            if (builder.Count > 0)
            {
                builder.Add(separator);
            }

            builder.Add(part);
        }

        return Concat(builder.DrainToImmutable());
    }
}
