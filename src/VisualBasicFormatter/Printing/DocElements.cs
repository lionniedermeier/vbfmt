using System.Collections.Immutable;

namespace VisualBasicFormatter.Printing;

/// <summary>Nothing at all. An absent optional child, or a break the language forbids.</summary>
internal sealed class DocNothing : Doc
{
    internal static readonly DocNothing Instance = new();

    private DocNothing() { }

    internal override bool Expands => false;
}

/// <summary>Literal output. Never contains a line break.</summary>
internal sealed class DocText : Doc
{
    internal DocText(string value) => Value = value;

    internal string Value { get; }

    internal override bool Expands => false;
}

/// <summary>
/// A break opportunity. The constructor is private so that a line can only be obtained from the
/// singletons on <see cref="Doc"/> -- in VB a break is legal only at specific points, and the rules
/// have to go through the language layer to get one.
/// </summary>
internal sealed class DocLine : Doc
{
    internal static readonly DocLine Soft = new(LineKind.Soft);
    internal static readonly DocLine SpaceOrBreak = new(LineKind.Space);
    internal static readonly DocLine Hard = new(LineKind.Hard);
    internal static readonly DocLine Empty = new(LineKind.Empty);

    private DocLine(LineKind kind) => Kind = kind;

    internal LineKind Kind { get; }

    internal override bool Expands => Kind is LineKind.Hard or LineKind.Empty;
}

/// <summary>A sequence.</summary>
internal sealed class DocConcat : Doc
{
    internal DocConcat(ImmutableArray<Doc> parts)
    {
        Parts = parts;

        foreach (var part in parts)
        {
            if (part.Expands)
            {
                Expands = true;
                break;
            }
        }
    }

    internal ImmutableArray<Doc> Parts { get; }

    internal override bool Expands { get; }
}

/// <summary>
/// The unit the printer decides flat-or-broken for. Content that expands makes the group -- and,
/// through <see cref="Doc.Expands"/>, every enclosing group -- break without being measured.
/// A broken group does <em>not</em> force its nested groups to break; each is measured again.
/// </summary>
internal sealed class DocGroup : Doc
{
    internal DocGroup(Doc content, bool shouldBreak = false)
    {
        Content = content;
        ShouldBreak = shouldBreak;
        Expands = content.Expands || shouldBreak;
    }

    internal Doc Content { get; }

    /// <summary>
    /// The decision taken in advance: this group breaks without being measured, even though nothing
    /// inside it demands a break. It is how a <see cref="DocConditionalGroup"/> states what one of
    /// its layouts looks like, and the only way to break a group without a hard line.
    /// </summary>
    internal bool ShouldBreak { get; }

    internal override bool Expands { get; }
}

/// <summary>
/// A choice between prepared layouts of the same content, from the most compact to the most
/// expanded. The printer takes the first whose <em>first line</em> still fits and falls back to the
/// last one, so an inner break can be tried before an outer one.
/// </summary>
internal sealed class DocConditionalGroup : Doc
{
    internal DocConditionalGroup(ImmutableArray<Doc> states) => States = states;

    /// <summary>Most compact first, most expanded last. Never empty.</summary>
    internal ImmutableArray<Doc> States { get; }

    /// <summary>
    /// Always false, and deliberately so. The expanded states carry forced breaks by construction;
    /// letting those reach the enclosing groups through <see cref="Doc.Expands"/> would break every
    /// ancestor before this choice is ever made. A break inside the chosen state still ends the line
    /// under test, because the printer measures each state itself.
    /// </summary>
    internal override bool Expands => false;
}

/// <summary>Content one indent level deeper.</summary>
internal sealed class DocIndent : Doc
{
    internal DocIndent(Doc content)
    {
        Content = content;
        Expands = content.Expands;
    }

    internal Doc Content { get; }

    internal override bool Expands { get; }
}

/// <summary>Content whose continuation lines start at the column the align was opened at.</summary>
internal sealed class DocAlign : Doc
{
    internal DocAlign(Doc content)
    {
        Content = content;
        Expands = content.Expands;
    }

    internal Doc Content { get; }

    internal override bool Expands { get; }
}

/// <summary>Different output depending on whether the enclosing group broke. The <c>_</c> marker.</summary>
internal sealed class DocConditional : Doc
{
    internal DocConditional(Doc whenBroken, Doc whenFlat)
    {
        WhenBroken = whenBroken;
        WhenFlat = whenFlat;

        // Only the flat branch can force a break; the broken branch is reached once the decision
        // has already been made.
        Expands = whenFlat.Expands;
    }

    internal Doc WhenBroken { get; }

    internal Doc WhenFlat { get; }

    internal override bool Expands { get; }
}

/// <summary>Content held back until the current line ends. A trailing <c>' comment</c>.</summary>
internal sealed class DocLineSuffix : Doc
{
    internal DocLineSuffix(Doc content) => Content = content;

    internal Doc Content { get; }

    internal override bool Expands => false;
}

/// <summary>
/// Source reproduced as it stands. Contains no break opportunity, which is what makes it safe for
/// XML literals, interpolated strings, directives and every node without a rule yet.
/// </summary>
internal sealed class DocVerbatim : Doc
{
    internal DocVerbatim(ImmutableArray<string> lines, VerbatimMode mode)
    {
        Lines = lines;
        Mode = mode;
        Expands = lines.Length > 1;
    }

    internal ImmutableArray<string> Lines { get; }

    internal VerbatimMode Mode { get; }

    internal override bool Expands { get; }
}

/// <summary>Forces every enclosing group to break without emitting anything itself.</summary>
internal sealed class DocExpandParent : Doc
{
    internal static readonly DocExpandParent Instance = new();

    private DocExpandParent() { }

    internal override bool Expands => true;
}

/// <summary>
/// Greedy filling: as many parts per line as fit, rather than the all-or-nothing of a group. Parts
/// alternate content and separator, starting and ending with content.
/// </summary>
internal sealed class DocFill : Doc
{
    internal DocFill(ImmutableArray<Doc> parts, bool strict = false)
    {
        Parts = parts;
        Strict = strict;
        Expands = parts.Any(p => p.Expands);
    }

    internal ImmutableArray<Doc> Parts { get; }

    /// <summary>
    /// Measure the parts flat instead of in the enclosing mode. Off, a part that could wrap itself
    /// counts as fitting, which is what keeps a last-resort break out of a line an ordinary one
    /// already solves. On, that same leniency would let every part of a list count as fitting and
    /// nothing would ever be reflowed, so a list asks for the strict measure.
    /// </summary>
    internal bool Strict { get; }

    internal override bool Expands { get; }
}

internal sealed class DocDedentToRoot : Doc
{
    internal DocDedentToRoot(Doc content)
    {
        Content = content;
        Expands = content.Expands;
    }

    internal Doc Content { get; }

    internal override bool Expands { get; }
}

internal sealed class DocRootChoice : Doc
{
    internal DocRootChoice(Doc preferred, Doc fallback)
    {
        Preferred = preferred;
        Fallback = fallback;
        Expands = preferred.Expands;
    }

    internal Doc Preferred { get; }

    internal Doc Fallback { get; }

    internal override bool Expands { get; }
}

internal sealed class DocWholeFit : Doc
{
    internal DocWholeFit(Doc content)
    {
        Content = content;
        Expands = content.Expands;
    }

    internal Doc Content { get; }

    internal override bool Expands { get; }
}
