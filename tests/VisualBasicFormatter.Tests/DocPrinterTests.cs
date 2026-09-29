using System.Collections.Immutable;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Tests;

/// <summary>The IR and the printer on their own, without any VB in the picture.</summary>
public sealed class DocPrinterTests
{
    private static PrintOptions Options(
        int printWidth = 80,
        int indentSize = 4,
        bool useTabs = false
    ) =>
        new()
        {
            PrintWidth = printWidth,
            IndentSize = indentSize,
            UseTabs = useTabs,
            NewLine = "\n",
        };

    private static string Print(Doc document, PrintOptions? options = null) =>
        DocPrinter.Print(document, options ?? Options());

    private static Doc RootChoiceDoc(string tail) =>
        Doc.Concat(
            Doc.Text("If "),
            Doc.RootChoice(
                Doc.Indent(
                    Doc.Concat(
                        Doc.Text("a("),
                        Doc.Indent(Doc.HardLine, Doc.Text(tail)),
                        Doc.DedentToRoot(Doc.HardLine),
                        Doc.Text(")")
                    )
                ),
                Doc.Text("fallback")
            )
        );

    [Fact]
    public void RootChoiceTakesTheFallbackWhenTheRootBreaks()
    {
        Assert.Equal("If fallback", Print(RootChoiceDoc("x")));
    }

    [Fact]
    public void RootChoiceKeepsThePreferredWhenNothingBreaksAtTheRoot()
    {
        var doc = Doc.Concat(
            Doc.Text("If "),
            Doc.RootChoice(
                Doc.Indent(Doc.Concat(Doc.Text("a"), Doc.HardLine, Doc.Text("b"))),
                Doc.Text("fallback")
            )
        );

        Assert.Equal("If a\n    b", Print(doc));
    }

    [Fact]
    public void DedentToRootWithoutARootChangesNothing()
    {
        var doc = Doc.Indent(Doc.Concat(Doc.Text("a"), Doc.DedentToRoot(Doc.HardLine), Doc.Text("b")));

        Assert.Equal("a\n    b", Print(doc));
    }

    [Fact]
    public void PrintsAGroupFlatWhenItFits()
    {
        var doc = Doc.Group(Doc.Concat(Doc.Text("abc"), Doc.SoftLine, Doc.Text("def")));

        Assert.Equal("abcdef", Print(doc));
    }

    [Fact]
    public void BreaksAGroupThatDoesNotFit()
    {
        var doc = Doc.Group(Doc.Concat(Doc.Text("abc"), Doc.SoftLine, Doc.Text("def")));

        Assert.Equal("abc\ndef", Print(doc, Options(printWidth: 4)));
    }

    /// <summary>A space line is a space when flat and a break when the group expands.</summary>
    [Fact]
    public void TurnsASpaceIntoABreak()
    {
        var doc = Doc.Group(Doc.Concat(Doc.Text("aaa"), Doc.Line, Doc.Text("bbb")));

        Assert.Equal("aaa bbb", Print(doc));
        Assert.Equal("aaa\nbbb", Print(doc, Options(printWidth: 5)));
    }

    /// <summary>A broken group does not drag its nested groups along; each is measured again.</summary>
    [Fact]
    public void RemeasuresNestedGroups()
    {
        var doc = Doc.Group(
            Doc.Concat(
                Doc.Text("aaa"),
                Doc.Line,
                Doc.Group(Doc.Concat(Doc.Text("b"), Doc.Line, Doc.Text("c")))
            )
        );

        Assert.Equal("aaa\nb c", Print(doc, Options(printWidth: 5)));
    }

    /// <summary>
    /// The shape a block argument prints in: break after the opener inside the indent, break before
    /// the closer outside it, so the closer lands back at the opener's own column. The group breaks
    /// from <see cref="Doc.HardLine"/> alone -- no <c>shouldBreak</c> is needed, and none is passed.
    /// </summary>
    [Fact]
    public void BreaksBeforeTheClosingBracket()
    {
        var doc = Doc.Group(
            Doc.Text("Foo("),
            Doc.Indent(Doc.SoftLine, Doc.Concat(Doc.Text("a"), Doc.HardLine, Doc.Text("b"))),
            Doc.SoftLine,
            Doc.Text(")")
        );

        Assert.Equal("Foo(\n    a\n    b\n)", Print(doc));
    }

    /// <summary>
    /// A group has to account for what still follows it on the line -- otherwise a parameter list
    /// stays flat although the <c>) As Double</c> behind it no longer fits.
    /// </summary>
    [Fact]
    public void IncludesTheRestOfTheLineInTheMeasurement()
    {
        var doc = Doc.Concat(
            Doc.Group(Doc.Concat(Doc.Text("ab"), Doc.SoftLine, Doc.Text("cd"))),
            Doc.Text("EFGH")
        );

        Assert.Equal("ab\ncdEFGH", Print(doc, Options(printWidth: 6)));
    }

    /// <summary>A hard line anywhere inside forces every enclosing group to break.</summary>
    [Fact]
    public void ForcesTheOuterGroupThroughAHardBreak()
    {
        var doc = Doc.Group(
            Doc.Concat(
                Doc.Text("a"),
                Doc.SoftLine,
                Doc.Group(Doc.Concat(Doc.Text("b"), Doc.HardLine, Doc.Text("c")))
            )
        );

        Assert.Equal("a\nb\nc", Print(doc));
    }

    [Fact]
    public void IndentsWithTabsAndCountsThemAsOneLevel()
    {
        var doc = Doc.Indent(
            Doc.HardLine,
            Doc.Group(Doc.Concat(Doc.Text("ab"), Doc.SoftLine, Doc.Text("cd")))
        );

        // Indent 4 + "abcd" is 8 and exceeds 6, so the group breaks. Were the tab counted as one
        // column the group would fit and stay flat.
        Assert.Equal("\n\tab\n\tcd", Print(doc, Options(printWidth: 6, useTabs: true)));
    }

    [Fact]
    public void AlignsAtTheCurrentColumn()
    {
        var doc = Doc.Group(
            Doc.Concat(
                Doc.Text("Foo("),
                Doc.Align(Doc.Concat(Doc.Text("a,"), Doc.Line, Doc.Text("b"))),
                Doc.Text(")")
            )
        );

        Assert.Equal("Foo(a,\n    b)", Print(doc, Options(printWidth: 6)));
    }

    [Fact]
    public void IndentsOneLevelDeeper()
    {
        var doc = Doc.Concat(
            Doc.Text("Sub S()"),
            Doc.Indent(Doc.HardLine, Doc.Text("x")),
            Doc.HardLine,
            Doc.Text("End Sub")
        );

        Assert.Equal("Sub S()\n    x\nEnd Sub", Print(doc));
    }

    [Fact]
    public void WritesNoSpacesOnAnEmptyLine()
    {
        var doc = Doc.Concat(Doc.Text("a"), Doc.Indent(Doc.EmptyLine, Doc.Text("b")));

        Assert.Equal("a\n\n    b", Print(doc));
    }

    [Fact]
    public void AppendsALineSuffixAtTheEndOfTheLine()
    {
        var doc = Doc.Concat(
            Doc.Text("code"),
            Doc.LineSuffix(Doc.Concat(Doc.Space, Doc.Text("' note"))),
            Doc.HardLine,
            Doc.Text("next")
        );

        Assert.Equal("code ' note\nnext", Print(doc));
    }

    /// <summary>A line suffix at the very end still has to be emitted.</summary>
    [Fact]
    public void EmitsALineSuffixEvenAtEndOfFile()
    {
        var doc = Doc.Concat(
            Doc.Text("code"),
            Doc.LineSuffix(Doc.Concat(Doc.Space, Doc.Text("' note")))
        );

        Assert.Equal("code ' note", Print(doc));
    }

    [Fact]
    public void EmitsTheConditionalPartOnlyWhenBreaking()
    {
        var doc = Doc.Group(
            Doc.Concat(
                Doc.Text("a"),
                Doc.Conditional(Doc.Text(" _"), Doc.Nothing),
                Doc.Line,
                Doc.Text("b")
            )
        );

        Assert.Equal("a b", Print(doc));
        Assert.Equal("a _\nb", Print(doc, Options(printWidth: 2)));
    }

    [Fact]
    public void FillsLinesGreedily()
    {
        var doc = Doc.Fill([Doc.Text("aa"), Doc.Line, Doc.Text("bb"), Doc.Line, Doc.Text("cc")]);

        Assert.Equal("aa bb\ncc", Print(doc, Options(printWidth: 5)));
    }

    /// <summary>
    /// Lenient filling lets a part that could wrap itself count as fitting; strict filling measures
    /// it flat, which is the only way a list of wrappable elements reflows at all.
    /// </summary>
    [Fact]
    public void MeasuresFlatUnderStrictFilling()
    {
        static Doc Parts(bool strict) =>
            Doc.Fill(
                [
                    Doc.Text("aa"),
                    Doc.Line,
                    Doc.Group(Doc.Concat(Doc.Text("bb"), Doc.SoftLine, Doc.Text("cc"))),
                ],
                strict
            );

        Assert.Equal("aa bb\ncc", Print(Parts(strict: false), Options(printWidth: 5)));
        Assert.Equal("aa\nbbcc", Print(Parts(strict: true), Options(printWidth: 5)));
    }

    /// <summary>A group may be told to break without anything inside it demanding one.</summary>
    [Fact]
    public void BreaksAGroupThatDemandsItEvenWhenItFits()
    {
        var doc = Doc.Group(Doc.Concat(Doc.Text("a"), Doc.Line, Doc.Text("b")), shouldBreak: true);

        Assert.Equal("a\nb", Print(doc));
    }

    [Fact]
    public void TakesTheFirstChoiceThatFits()
    {
        var doc = Doc.ConditionalGroup(Doc.Text("aaaaaaaa"), Doc.Text("bbbb"), Doc.Text("cc"));

        Assert.Equal("aaaaaaaa", Print(doc, Options(printWidth: 80)));
        Assert.Equal("bbbb", Print(doc, Options(printWidth: 5)));

        // Nothing fits, so the most expanded one is used anyway.
        Assert.Equal("cc", Print(doc, Options(printWidth: 1)));
    }

    /// <summary>
    /// The point of the whole construction: a layout keeps everything in front of its expanded part
    /// on the line, and only the break that layout declares is taken.
    /// </summary>
    [Fact]
    public void BreaksOnlyThePartTheChoiceProvidesFor()
    {
        var inner = Doc.Concat(Doc.Text("b"), Doc.Line, Doc.Text("c"));

        var doc = Doc.ConditionalGroup(
            Doc.Concat(Doc.Text("aaa "), Doc.Group(inner)),
            Doc.Concat(Doc.Text("aaa "), Doc.Group(inner, shouldBreak: true))
        );

        Assert.Equal("aaa b\nc", Print(doc, Options(printWidth: 5)));
    }

    /// <summary>
    /// A layout is only worth taking if it fits on the breaks it declares. The break a nested group
    /// might take does not count -- that group is measured again while printing and may stay flat,
    /// which would leave the line over the limit after the layout was already accepted.
    /// </summary>
    [Fact]
    public void MeasuresAChoiceOnlyByItsOwnBreaks()
    {
        var doc = Doc.ConditionalGroup(
            Doc.Concat(
                Doc.Text("aaaaaa "),
                Doc.Group(Doc.Concat(Doc.Text("b"), Doc.Line, Doc.Text("c")))
            ),
            Doc.Text("short")
        );

        Assert.Equal("short", Print(doc, Options(printWidth: 5)));
    }

    /// <summary>The forced break of a layout must not reach the groups above the choice.</summary>
    [Fact]
    public void DoesNotLetTheForcedBreakEscapeOutward()
    {
        var forced = Doc.Group(
            Doc.Concat(Doc.Text("b"), Doc.Line, Doc.Text("c")),
            shouldBreak: true
        );

        static Doc Around(Doc inner) => Doc.Group(Doc.Concat(Doc.Text("a"), Doc.Line, inner));

        // On its own the forced break reaches every enclosing group ...
        Assert.Equal("a\nb\nc", Print(Around(forced)));

        // ... but a choice absorbs it, so the group above is measured as usual and stays flat.
        Assert.Equal("a bc", Print(Around(Doc.ConditionalGroup(Doc.Text("bc"), forced))));
    }

    [Fact]
    public void IndentsAPreservedVerbatimRegion()
    {
        var doc = Doc.Indent(
            Doc.HardLine,
            Doc.Verbatim(["<x>", "  <y/>", "</x>"], VerbatimMode.Preserve)
        );

        Assert.Equal("\n    <x>\n      <y/>\n    </x>", Print(doc));
    }

    [Fact]
    public void KeepsTrailingWhitespaceInsideAnAnchoredVerbatimRegion()
    {
        var doc = Doc.Indent(
            Doc.HardLine,
            Doc.Verbatim(["<t>", "a   ", "</t>"], VerbatimMode.Anchored)
        );

        Assert.Equal("\n    <t>\na   \n</t>", Print(doc));
    }

    [Fact]
    public void TrimsTrailingWhitespaceInsideAPreservedVerbatimRegion()
    {
        var doc = Doc.Indent(
            Doc.HardLine,
            Doc.Verbatim(["<t>", "a   ", "</t>"], VerbatimMode.Preserve)
        );

        Assert.Equal("\n    <t>\n    a\n    </t>", Print(doc));
    }

    /// <summary>
    /// Raw content owns its columns, the first line included -- the indent already written for it
    /// is taken back. Anything else would indent the region once more on every pass.
    /// </summary>
    [Fact]
    public void LeavesARawVerbatimRegionAlone()
    {
        var doc = Doc.Indent(
            Doc.HardLine,
            Doc.Verbatim(["#If DEBUG Then", "  whatever", "#End If"], VerbatimMode.Raw)
        );

        Assert.Equal("\n#If DEBUG Then\n  whatever\n#End If", Print(doc));
    }

    /// <summary>A verbatim region spans lines, so no enclosing group may be measured as flat.</summary>
    [Fact]
    public void ForcesTheOuterGroupThroughAMultiLineRegion()
    {
        var doc = Doc.Group(
            Doc.Concat(
                Doc.Text("Foo("),
                Doc.SoftLine,
                Doc.Verbatim(["a", "b"], VerbatimMode.Preserve),
                Doc.Text(")")
            )
        );

        Assert.Equal("Foo(\na\nb)", Print(doc));
    }

    [Fact]
    public void BreaksAsSoonAsTheLimitIsPassed()
    {
        static Doc Row(int width) =>
            Doc.Group(Doc.Concat(Doc.Text(new string('a', width - 4)), Doc.Line, Doc.Text("bbb")));

        Assert.DoesNotContain('\n', Print(Row(80)));
        Assert.Contains('\n', Print(Row(81)));
    }

    [Fact]
    public void DropsEmptyParts()
    {
        var doc = Doc.Concat(Doc.Nothing, Doc.Text("a"), Doc.Nothing, Doc.Text("b"), Doc.Nothing);

        Assert.Equal("ab", Print(doc));
        Assert.IsType<DocConcat>(doc);
        Assert.Equal(2, ((DocConcat)doc).Parts.Length);
    }

    [Fact]
    public void JoinsPartsWithASeparator()
    {
        var doc = Doc.Join(Doc.Text(", "), [Doc.Text("a"), Doc.Text("b"), Doc.Text("c")]);

        Assert.Equal("a, b, c", Print(doc));
    }

    [Fact]
    public void BreaksEvenAtAnImpossiblyNarrowLimit()
    {
        var printed = DocPrinter.Print(
            Doc.Group(Doc.Concat(Doc.Text("a"), Doc.SoftLine, Doc.Text("b"))),
            Options(printWidth: 1)
        );

        Assert.Equal("a\nb", printed);
    }

    [Fact]
    public void ProducesAnEmptyPartFromAnEmptyList()
    {
        Assert.Same(Doc.Nothing, Doc.Concat(ImmutableArray<Doc>.Empty));
        Assert.Same(Doc.Nothing, Doc.Text(string.Empty));
        Assert.Same(Doc.Nothing, Doc.Group(Doc.Nothing));
        Assert.Same(Doc.Nothing, Doc.Indent(Doc.Nothing));
    }
}
