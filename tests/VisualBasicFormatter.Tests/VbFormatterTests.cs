using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace VisualBasicFormatter.Tests;

public sealed class VbFormatterTests
{
    /// <summary>Set <c>VBNET_FORMAT_UPDATE_GOLDEN=1</c> to rewrite the expectation files.</summary>
    private static bool UpdateGolden =>
        Environment.GetEnvironmentVariable("VBNET_FORMAT_UPDATE_GOLDEN") == "1";

    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void FormatsAsExpected(string name)
    {
        var result = VbFormatter.Format(TestCases.ReadInput(name));
        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));

        var path = TestCases.ExpectedPath(name);
        if (UpdateGolden)
        {
            File.WriteAllText(path, result.Text);
            return;
        }

        Assert.Equal(File.ReadAllText(path), result.Text);
    }

    /// <summary>
    /// A line may end before a closing bracket, so a block argument can have the bracket below it.
    /// This is the assumption the block-argument layout rests on -- asserted on the parser, not on
    /// the formatter.
    /// </summary>
    [Fact]
    public void AcceptsABreakBeforeTheBracket()
    {
        const string Source = """
            Module M

                Public Sub S()
                    Register(
                        Function(tag)
                            Return tag.Length
                        End Function
                    )
                    Handlers = {
                        Sub()
                            Work()
                        End Sub
                    }
                End Sub

            End Module

            """;

        var result = VbFormatter.Format(Source.ReplaceLineEndings("\r\n"));

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
    }

    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void IsIdempotent(string name)
    {
        var once = VbFormatter.Format(TestCases.ReadInput(name)).Text;
        var twice = VbFormatter.Format(once);

        Assert.Equal(once, twice.Text);
        Assert.False(twice.Changed);
    }

    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void DoesNotChangeTheCode(string name)
    {
        var input = TestCases.ReadInput(name);
        var result = VbFormatter.Format(input);

        var before = Parse(input);
        var after = Parse(result.Text);

        // Imports are resorted and deduplicated, so compare them as a set.
        Assert.Equal(ImportClauses(before), ImportClauses(after));
        Assert.True(
            before.WithImports(default).IsEquivalentTo(after.WithImports(default), topLevel: false)
                || VbFormatter.StructurallyIdentical(
                    before.WithImports(default),
                    after.WithImports(default)
                )
        );
    }

    [Fact]
    public void LeavesAlreadyFormattedSourceUnchanged()
    {
        var input = TestCases.ReadInput("AlreadyFormatted");
        var result = VbFormatter.Format(input);

        Assert.False(result.Changed);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void KeepsTheFileHeaderOnTop()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("FileHeader"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Equal("' Copyright (c) Contoso AG.", lines[0]);
        Assert.Equal("' Licensed under the MIT license.", lines[1]);
    }

    [Fact]
    public void SortsImportsAndRemovesDuplicates()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("UnsortedImports"));
        var imports = Parse(result.Text).Imports.Select(i => i.ToString().Trim()).ToList();

        Assert.Equal(
            [
                "Imports System",
                "Imports System.Collections.Generic",
                "Imports System.Text",
                "Imports Newtonsoft.Json",
                "Imports IO = System.IO",
            ],
            imports
        );
    }

    [Fact]
    public void SortsImportsAroundAPreprocessorDirective()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("ConditionalImports"));

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Contains("#If Not DEBUG Then", result.Text);
        Assert.Contains("#End If", result.Text);
    }

    [Fact]
    public void LeavesImportsAloneWhenDisabled()
    {
        var input = TestCases.ReadInput("UnsortedImports");
        var result = VbFormatter.Format(input, new FormatterOptions { OrganizeImports = false });

        Assert.Equal("Imports System.Text", Parse(result.Text).Imports[0].ToString().Trim());
    }

    /// <summary>Proves that the indentation actually comes from <see cref="FormatterOptions"/>.</summary>
    [Fact]
    public void HonorsTheIndentWidth()
    {
        const string Source = """
            Module M
            Sub S()
            Dim x = 1
            End Sub
            End Module
            """;

        var result = VbFormatter.Format(Source, new FormatterOptions { IndentSize = 2 });
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Equal("  Sub S()", lines[1]);
        Assert.Equal("    Dim x = 1", lines[2]);
    }

    /// <summary>
    /// <see cref="EndOfLine.Auto"/> follows the input; the other two say what they mean, whatever the
    /// input used.
    /// </summary>
    [Theory]
    [InlineData(EndOfLine.Auto, "\n", "\n")]
    [InlineData(EndOfLine.Auto, "\r\n", "\r\n")]
    [InlineData(EndOfLine.Lf, "\r\n", "\n")]
    [InlineData(EndOfLine.CrLf, "\n", "\r\n")]
    public void WritesTheRequestedLineEnding(EndOfLine endOfLine, string input, string expected)
    {
        var source = string.Join(
            input,
            "Module M",
            "",
            "    Sub S()",
            "    End Sub",
            "",
            "End Module",
            ""
        );

        var result = VbFormatter.Format(source, new FormatterOptions { EndOfLine = endOfLine });

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Equal(
            expected,
            result.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n"
        );
    }

    [Fact]
    public void DoesNotTouchUnparsableSource()
    {
        const string Broken = "Module M\r\nSub S(\r\nEnd Module\r\n";

        var result = VbFormatter.Format(Broken);

        Assert.True(result.HasErrors);
        Assert.False(result.Changed);
        Assert.Equal(Broken, result.Text);
    }

    /// <summary>
    /// Every break is an implicit continuation, so no underscore may ever be emitted -- the explicit
    /// <c>_</c> is not something vbfmt writes at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void EmitsNoUnderscore(string name)
    {
        var result = VbFormatter.Format(TestCases.ReadInput(name));

        Assert.DoesNotContain(
            result.Text.ReplaceLineEndings("\n").Split('\n'),
            l => l.TrimEnd().EndsWith(" _", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void BreaksCallChainsAfterTheDot()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("MemberChain"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        // Plain property hops stay together; only the invoked links break.
        Assert.Equal("        Dim companies = State.Companies.Values.", lines[2]);
        Assert.Equal("            Where(AddressOf FilterDivision).", lines[3]);
    }

    /// <summary>
    /// A block argument cannot be laid out behind the bracket that holds it: its body would start
    /// one level further in than the bracket's own indent, leaving it that much less width. It goes
    /// below the bracket instead, one indent level in.
    /// </summary>
    [Fact]
    public void PlacesABlockBelowTheBracket()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaArgument"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        var open = Array.FindIndex(
            lines,
            l => l.TrimEnd().EndsWith("Any(", StringComparison.Ordinal)
        );
        Assert.True(open >= 0);

        var indent = lines[open][..(lines[open].Length - lines[open].TrimStart().Length)];
        Assert.Equal(indent + "    Function(tag)", lines[open + 1]);
    }

    /// <summary>
    /// The whole point of moving the block below the bracket: its own argument lists get their width
    /// back and no longer have to break themselves.
    /// </summary>
    [Fact]
    public void KeepsABlocksBodyWithinTheLimit()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaArgument"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.DoesNotContain(lines, l => l.Trim() == "\"F\",");
    }

    /// <summary>The closing bracket goes back to the list's own indent, below the block.</summary>
    [Fact]
    public void ClosesTheBracketAfterABlockOnItsOwnLine()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaArgument"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        var open = Array.FindIndex(
            lines,
            l => l.TrimEnd().EndsWith("Any(", StringComparison.Ordinal)
        );
        var close = Array.FindIndex(lines, open, lines.Length - open, l => l.Trim() == ")");
        Assert.True(open >= 0 && close > open);

        var openIndent = lines[open][..(lines[open].Length - lines[open].TrimStart().Length)];
        var closeIndent = lines[close][..(lines[close].Length - lines[close].TrimStart().Length)];
        Assert.Equal(openIndent, closeIndent);
    }

    /// <summary>A collection initializer's brace gets the same closing-on-its-own-line treatment.</summary>
    [Fact]
    public void PlacesAnInitializersBraceOnItsOwnLine()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaArgument"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(lines, l => l.Trim() == "}");
    }

    /// <summary>
    /// Every list that breaks closes on a line of its own, not only one holding a block. This is the
    /// shape <c>docs/standard_format.md</c> specifies, and it is not a per-construct decision.
    /// </summary>
    [Fact]
    public void ClosesEveryBrokenListOnItsOwnLine()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LongArgumentList"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(lines, l => l.Trim() == ")");

        // And at the indent of the line that opened it, not the elements'.
        var open = Array.FindIndex(
            lines,
            l => l.TrimEnd().EndsWith("String.Format(", StringComparison.Ordinal)
        );
        var close = Array.FindIndex(lines, open, lines.Length - open, l => l.Trim() == ")");
        Assert.True(open >= 0 && close > open);
        Assert.Equal(Indent(lines[open]), Indent(lines[close]));
    }

    /// <summary>
    /// A Sub lambda gets its header on its own line, exactly like a Function lambda. It used to be
    /// excluded because the move tripped a Roslyn equivalence quirk; that no longer reproduces, so
    /// the layout now matches the lambda example in <c>docs/standard_format.md</c>.
    /// </summary>
    [Fact]
    public void PlacesASubBlockBelowTheBracketToo()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaArgument"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.DoesNotContain(
            lines,
            l => l.TrimEnd().EndsWith("Register(Sub()", StringComparison.Ordinal)
        );
        Assert.Contains(lines, l => l.Trim() == "Sub()");
    }

    [Fact]
    public void FormatsAroundACommentInABlockArgument()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaBlock"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Contains(lines, l => l.Trim() == "paramA,");
        Assert.Contains(
            lines,
            l =>
                l.Trim()
                == "' Lambda content with several nested layers that are longer than printWidth"
        );
    }

    [Fact]
    public void HangsALambdaAfterAContinuationPoint()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaBlock"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));

        var assignment = Array.FindIndex(lines, l => l.Trim() == "Dim handler =");
        Assert.True(assignment >= 0);
        Assert.Equal(Indent(lines[assignment]) + 4, Indent(lines[assignment + 1]));
        Assert.Equal("Function(value As Integer)", lines[assignment + 1].Trim());

        var handler = Array.FindIndex(lines, l => l.Trim() == "AddHandler btn.Click,");
        Assert.True(handler >= 0);
        Assert.Equal(Indent(lines[handler]) + 4, Indent(lines[handler + 1]));
        Assert.Equal("Sub(s, e)", lines[handler + 1].Trim());
    }

    [Fact]
    public void KeepsAligningALambdaWithoutAContinuationPoint()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LambdaBlock"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Contains(lines, l => l.TrimEnd().EndsWith("Return Sub()", StringComparison.Ordinal));
    }

    [Fact]
    public void BreaksTheCommaAndTheDotBeforeTheLambdaParameterList()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("SingleLineLambda"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Contains(
            lines,
            l => l.Trim().StartsWith("Sub(s, e) CollectionTypeStuff", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(lines, l => l.Trim() == "s, e");
    }

    [Fact]
    public void KeepsTheDotGluedWhenTheArgumentListCanBreakInstead()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("SingleLineLambda"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Contains(lines, l => l.Trim() == "logger.LogInformationWithContext(");
    }

    [Fact]
    public void HangsALambdaAssignmentButNotAPlainCallAssignment()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("SingleLineLambda"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));

        var assignment = Array.FindIndex(
            lines,
            l => l.Trim() == "Dim lambdaWithIfOperator________________________ ="
        );
        Assert.True(assignment >= 0);
        Assert.Equal(Indent(lines[assignment]) + 4, Indent(lines[assignment + 1]));
        Assert.StartsWith("Function(value As Integer?)", lines[assignment + 1].Trim());

        Assert.Contains(
            lines,
            l =>
                l.Trim()
                    .StartsWith(
                        "Dim plainCallAssignment_________________________________ = Compute(",
                        StringComparison.Ordinal
                    )
        );
    }

    /// <summary>
    /// A list that breaks first tries every element on one indented line, and only stacks them when
    /// that line does not fit either.
    /// </summary>
    [Fact]
    public void PacksACallOntoOneIndentedLineBeforeStackingIt()
    {
        const string Source = """
            Module M

                Public Sub S()
                    SaveTankMeasurementRecord(tankIdentifierValue, currentVolumeReading, recordedTimestampUtc, auditTrailReference, operatorDisplayName)
                    SaveTankMeasurementRecord(tankIdentifierValue, currentVolumeReading, recordedTimestampUtc, auditTrailReference, operatorDisplayName, siteIdentifierValue, calibrationOffsetValue)
                End Sub

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        // Too wide to stay on the statement's line, but the arguments still fit on one indented
        // line of their own -- so they take it rather than stacking.
        Assert.Contains(
            lines,
            l =>
                l.Trim()
                == "tankIdentifierValue, currentVolumeReading, recordedTimestampUtc, "
                    + "auditTrailReference, operatorDisplayName"
        );

        // Two more arguments and that line no longer fits either: one element per line.
        Assert.Contains(lines, l => l.Trim() == "operatorDisplayName,");
        Assert.Contains(lines, l => l.Trim() == "calibrationOffsetValue");
    }

    /// <summary>
    /// The packed layout is for signatures and calls only. A list of named things -- an initializer,
    /// a type parameter list -- always stacks, so that adding one is a one-line diff.
    /// </summary>
    [Fact]
    public void NeverPacksAnInitializer()
    {
        const string Source = """
            Module M

                Public Sub S()
                    Dim tank = New Tank With {.Identifier = 1, .CurrentVolume = 240.5, .Label = "the primary tank"}
                End Sub

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"), new FormatterOptions { PrintWidth = 60 })
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(lines, l => l.Trim() == ".Identifier = 1,");
        Assert.Contains(lines, l => l.Trim() == ".CurrentVolume = 240.5,");
        Assert.Contains(lines, l => l.Trim() == "}");
    }

    /// <summary>The leading dot of a With block needs an underscore, so it must stay put.</summary>
    [Fact]
    public void LeavesTheLeadingDotOfAWithBlockInPlace()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("WithBlock"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(
            lines,
            l => l.TrimStart().StartsWith(".Diagnostics", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            lines,
            l => l.TrimEnd().EndsWith("With configuration.", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// A comma outranks an operator: the condition of the ternary stays whole and the outer
    /// <c>If</c>'s commas break, rather than the line being split at the <c>&gt;</c>.
    /// </summary>
    [Fact]
    public void PrefersBreakingAtTheCommaOverTheOperator()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("NestedTernary"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        // No line may end at a comparison operator.
        Assert.DoesNotContain(lines, l => l.TrimEnd().EndsWith(">", StringComparison.Ordinal));
    }

    /// <summary>
    /// The outer list breaks before the nested one: the line holding the head of a nested ternary
    /// ends at the opening paren of the trailing <c>If</c>, not partway into its arguments.
    /// </summary>
    [Fact]
    public void BreaksTheOuterListBeforeTheInner()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("NestedTernary"));

        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');
        var head = Array.Find(lines, l => l.Contains("Dim nestedConditional"));

        Assert.NotNull(head);
        Assert.EndsWith("If(", head!.TrimEnd());
    }

    /// <summary>
    /// The clauses of a query align under the keyword that opens it rather than hanging below the
    /// statement, which is what the VB style guide asks for.
    /// </summary>
    [Fact]
    public void AlignsQueryClausesUnderTheHead()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("XmlLiteralInline"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        var head = Array.FindIndex(
            lines,
            l => l.Contains("<%= From e In _employees", StringComparison.Ordinal)
        );
        Assert.True(head >= 0);

        var column = lines[head].IndexOf("From", StringComparison.Ordinal);
        Assert.Equal(new string(' ', column) + "Select", lines[head + 1][..(column + 6)]);
    }

    /// <summary>
    /// A join too long for its line breaks in front of <c>On</c>, which is the one keyword inside a
    /// query clause that a break is offered at.
    /// </summary>
    [Fact]
    public void BreaksAJoinBeforeItsCondition()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LinqQueryClauses"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        var condition = Array.FindIndex(
            lines,
            l =>
                l.TrimStart()
                    .StartsWith("On employee.ReportingManagerIdentifier", StringComparison.Ordinal)
        );

        Assert.True(condition > 0);
        Assert.EndsWith("Join reportingManager In employees", lines[condition - 1]);
    }

    /// <summary>A join short enough for its line keeps its condition behind it.</summary>
    [Fact]
    public void LeavesAFittingJoinOnOneLine()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LinqQueryClauses"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(
            lines,
            l =>
                l.TrimStart()
                    .StartsWith(
                        "Join department In departments On employee.DepartmentId",
                        StringComparison.Ordinal
                    )
        );
    }

    /// <summary>A query breaks in front of its clause keywords, and needs no underscore for it.</summary>
    [Fact]
    public void BreaksAQueryBeforeTheClause()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LinqQuery"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(
            lines,
            l => l.TrimStart().StartsWith("Where employee.Salary", StringComparison.Ordinal)
        );
        Assert.Contains(
            lines,
            l => l.TrimStart().StartsWith("Order By employee.Salary", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void KeepsAFittingQueryOnTheStatementLine()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LinqQuery"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(
            lines,
            l => l.Trim() == "Dim names = From employee In employees Select employee.Name"
        );
    }

    [Fact]
    public void HangsALongQueryBelowTheAssignment()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("LinqQuery"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        var head = Array.FindIndex(
            lines,
            l => l.Contains("Dim quarterlyHeadcountByDepartment", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.EndsWith("=", lines[head]);

        var query = lines[head + 1].TrimStart();
        Assert.StartsWith("From employee In employees Where", query, StringComparison.Ordinal);
        Assert.Contains("Select employee.Name", query, StringComparison.Ordinal);
    }

    /// <summary>
    /// A parameter's attribute is not a declaration's: it sits inside a bracketed list, where a line
    /// of its own would only be a worse layout.
    /// </summary>
    [Fact]
    public void LeavesAParameterAttributeOnItsLine()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("Attributes"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(
            lines,
            l =>
                l.TrimStart()
                    .StartsWith("Public Sub Write(<Out> ByRef count", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Breaking behind the opening bracket would leave it alone on a line and buy nothing; the
    /// commas inside the list are what carries the break.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void LeavesTheBracketOfAnAttributeListNotAlone(string name)
    {
        var result = VbFormatter.Format(TestCases.ReadInput(name));

        Assert.DoesNotContain(
            result.Text.ReplaceLineEndings("\n").Split('\n'),
            l => l.TrimEnd().EndsWith("<", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// The attributes of a tag hang one level below it, and the closing bracket stays on the last of
    /// them rather than taking a line of its own; see <c>docs/standard_format.md</c>.
    /// </summary>
    [Fact]
    public void HangsXmlAttributesBelowTheTag()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("XmlLiteralAttributes"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        var head = Array.FindIndex(
            lines,
            l => l.TrimEnd().EndsWith("<employee", StringComparison.Ordinal)
        );
        Assert.True(head >= 0);

        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 1]));
        Assert.StartsWith("id=", lines[head + 1].TrimStart(), StringComparison.Ordinal);
        Assert.Equal(Indent(lines[head + 1]), Indent(lines[head + 2]));
        Assert.StartsWith("name=", lines[head + 2].TrimStart(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A literal that fits stays behind the <c>=</c>; one that does not drops below it and is tried
    /// again there; only then is it broken open. A literal holding a child element skips to the last
    /// of the three, however much room the line still had.
    /// </summary>
    [Fact]
    public void WrapsALiteralInThreeSteps()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("XmlLiteralWrapping"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(lines, l => l.Trim() == "Dim fits = <person id=\"42\" name=\"Alice\"/>");
        Assert.Contains(lines, l => l.Trim() == "Dim hangs =");
        Assert.Contains(
            lines,
            l => l.Trim().StartsWith("<person id=\"42\"", StringComparison.Ordinal)
        );
        Assert.Contains(lines, l => l.Trim() == "Dim nested =");
    }

    /// <summary>
    /// A literal is one decision: once it breaks, every tag with attributes and every element with
    /// content in it breaks too, however much room the line still had.
    /// </summary>
    [Fact]
    public void BreaksAWholeLiteralOrNoneOfIt()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("XmlLiteralWrapping"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.DoesNotContain(lines, l => l.Contains("<address city=", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Trim() == "<address");
        Assert.Contains(lines, l => l.Trim() == "city=\"Berlin\"/>");
    }

    /// <summary>
    /// Text content makes the whitespace around it the author's. The element carrying it keeps every
    /// character, even while the elements around it are laid out afresh.
    /// </summary>
    [Fact]
    public void DoesNotTouchTextContent()
    {
        var result = VbFormatter.Format(TestCases.ReadInput("XmlLiteralText"));
        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        // The prose keeps its one line, the mixed content its own indentation.
        Assert.Contains(lines, l => l.Contains("<body>A paragraph with plenty of text"));
        Assert.Contains(
            lines,
            l => l == "                   Hello <%= name %>, glad you could make it."
        );
        Assert.Contains(lines, l => l == "   and    one   more</pre>");
    }

    [Fact]
    public void HangsABrokenConcatenationBelowTheAssignment()
    {
        var lines = Lines("StringConcatenation");

        var head = Array.FindIndex(
            lines,
            l => l.Contains("Dim longSqlQueryExpressionAsString", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.EndsWith("=", lines[head]);
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 1]));
        Assert.Equal("\"select id, name, address, salary\" &", lines[head + 1].TrimStart());
        Assert.Equal(Indent(lines[head + 1]), Indent(lines[head + 2]));
        Assert.Equal("\"from employees\" &", lines[head + 2].TrimStart());
        Assert.Equal(Indent(lines[head + 1]), Indent(lines[head + 3]));
        Assert.Equal("\"where salary > 80000\"", lines[head + 3].TrimStart());
    }

    [Fact]
    public void DoesNotCollapseADeliberatelyWrappedConcatenation()
    {
        var lines = Lines("StringConcatenation");

        var head = Array.FindIndex(
            lines,
            l => l.Contains("Dim deliberatelyWrappedQuery", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.EndsWith("=", lines[head]);
        Assert.Equal("\"select id, name, address, salary\" &", lines[head + 1].TrimStart());
        Assert.Equal("\"from employees\"", lines[head + 2].TrimStart());
    }

    [Fact]
    public void KeepsFragmentsTheAuthorPairedOnOneLine()
    {
        var lines = Lines("StringConcatenation");

        Assert.Contains(lines, l => l.Trim() == "\" from t\" & vbCrLf &");
        Assert.Contains(lines, l => l.Trim() == "\" where x = \" & identifier");
    }

    [Fact]
    public void CollapsesAShortConcatenationTheAuthorLeftOnOneLine()
    {
        var lines = Lines("StringConcatenation");

        Assert.Contains(
            lines,
            l =>
                l.Trim()
                == "Dim sqlQueryExpressionAsString = \"select id, name, address, salary\" & \"from employees\""
        );
    }

    [Fact]
    public void LeavesAWrappedConditionToTheWidth()
    {
        const string Source = """
            Module M

                Public Function F(ByVal candidate As Contract) As Boolean
                    Dim ok = candidate.IsActive AndAlso
                        candidate.HasValidSignature
                    Return ok
                End Function

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(
            lines,
            l => l.Trim() == "Dim ok = candidate.IsActive AndAlso candidate.HasValidSignature"
        );
    }

    [Fact]
    public void DoubleIndentsAWrappedIfConditionBelowItsBody()
    {
        var lines = Lines("BlockConditions");

        var head = Array.FindIndex(
            lines,
            l => l.TrimStart().StartsWith("If ", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.EndsWith("AndAlso", lines[head].TrimEnd());
        Assert.Equal(Indent(lines[head]) + 8, Indent(lines[head + 1]));
        Assert.Equal("candidate.HasValidSignature AndAlso", lines[head + 1].TrimStart());
        Assert.Equal(Indent(lines[head + 1]), Indent(lines[head + 3]));
        Assert.EndsWith("Then", lines[head + 3].TrimEnd());
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 4]));
        Assert.Equal("Return \"A\"", lines[head + 4].TrimStart());
    }

    [Fact]
    public void DoubleIndentsAWrappedLoopUntilCondition()
    {
        var lines = Lines("BlockConditions");

        var head = Array.FindIndex(
            lines,
            l => l.TrimStart().StartsWith("Loop Until ", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.EndsWith("OrElse", lines[head].TrimEnd());
        Assert.Equal(Indent(lines[head]) + 8, Indent(lines[head + 1]));
        Assert.Equal("candidate.HasExpired OrElse", lines[head + 1].TrimStart());
    }

    [Fact]
    public void DoubleIndentsAWrappedForEachCollectionBelowItsBody()
    {
        var lines = Lines("ForEachHeader");

        var head = Array.FindIndex(
            lines,
            l =>
                l.TrimStart()
                    .StartsWith("For Each element In elementStore.", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]) + 8, Indent(lines[head + 1]));
        Assert.Equal("ResolvePrimaryElements().", lines[head + 1].TrimStart());

        var body = Array.FindIndex(lines, head, l => l.Trim() == "element.Activate()");

        Assert.True(body > head);
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[body]));
    }

    [Fact]
    public void LeavesTheNumericForBoundAtASingleIndent()
    {
        var lines = Lines("ForEachHeader");

        var head = Array.FindIndex(
            lines,
            l => l.TrimStart().StartsWith("For i = 0 To elementStore.", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 1]));
        Assert.Equal("ResolvePrimaryElements().", lines[head + 1].TrimStart());
    }

    [Fact]
    public void BreaksBeforeTheLastCallOfAHeaderWhenItFitsOnItsOwnLine()
    {
        var lines = Lines("BlockConditions");

        var head = Array.FindIndex(
            lines,
            l => l.Trim() == "Short_."
        );

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]), Indent(lines[head + 1]));
        Assert.StartsWith("Call(", lines[head + 1].TrimStart());
        Assert.EndsWith("Then", lines[head + 1].TrimEnd());
    }

    [Fact]
    public void PutsTheFinalCloserOfAWrappedHeaderOnTheHeaderColumn()
    {
        var lines = Lines("BlockConditions");

        var head = Array.FindIndex(lines, l => l.Trim().StartsWith("If first", StringComparison.Ordinal));
        head = Array.FindIndex(lines, head + 1, l => l.Trim().StartsWith("If first", StringComparison.Ordinal));

        Assert.True(head >= 0);
        var close = Array.FindIndex(lines, head, l => l.Trim() == ") Then");

        Assert.True(close > head);
        Assert.Equal(Indent(lines[head]), Indent(lines[close]));
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 1]));
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[close + 1]));
    }

    [Fact]
    public void LeavesABracketedConditionOnTheHeaderColumn()
    {
        var lines = Lines("BlockConditions");

        var head = Array.FindIndex(lines, l => l.Trim() == "If Evaluate(");

        Assert.True(head >= 0);
        Assert.Equal(") Then", lines[head + 2].Trim());
        Assert.Equal(Indent(lines[head]), Indent(lines[head + 2]));
    }

    [Fact]
    public void BreaksBeforeTheClosingParenOfAWrappedCondition()
    {
        var lines = Lines("ParenthesizedConditions");

        var head = Array.FindIndex(lines, l => l.Trim() == "If (candidate.IsActive AndAlso");

        Assert.True(head >= 0);
        var close = Array.FindIndex(lines, head, l => l.Trim() == ") Then");

        Assert.True(close > head);
        Assert.Equal(Indent(lines[head]), Indent(lines[close]));
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 1]));
    }

    [Fact]
    public void OpensAWrappedConditionRatherThanOnlyItsCloser()
    {
        var lines = Lines("ParenthesizedConditions");

        var head = Array.FindIndex(
            lines,
            l => l.Trim().StartsWith("ElseIf (", StringComparison.Ordinal)
        );

        Assert.True(head >= 0);
        Assert.EndsWith("AndAlso", lines[head].TrimEnd());

        var close = Array.FindIndex(lines, head, l => l.Trim() == ") Then");

        Assert.True(close > head);
        Assert.Equal(Indent(lines[head]), Indent(lines[close]));
    }

    [Fact]
    public void KeepsEveryWrappedConditionWithinTheWidth()
    {
        var lines = Lines("ParenthesizedConditions");
        var limit = new FormatterOptions().PrintWidth;

        Assert.All(lines, l => Assert.True(l.Length <= limit, l));
    }

    [Fact]
    public void LeavesAnUnbreakableConditionLong()
    {
        const string Source = """
            Module M

                Public Function F(ByVal candidate As Contract) As Boolean
                    If (candidate.ThisSinglePropertyAccessHasNoBreakPointOfItsOwnAndSoOverflowsTheLineRegardlessOfAnythingElse) Then
                        Return True
                    End If

                    Return False
                End Function

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(
            lines,
            l =>
                l.Trim()
                == "If (candidate.ThisSinglePropertyAccessHasNoBreakPointOfItsOwnAndSoOverflowsTheLineRegardlessOfAnythingElse) Then"
        );
        Assert.DoesNotContain(lines, l => l.Trim() is ")" or ") Then");
    }

    [Fact]
    public void DoubleIndentsWrappedCaseClausesBelowTheCaseBody()
    {
        var lines = Lines("CaseClauses");

        var head = Array.FindIndex(lines, l => l.Trim() == "Case Feature.AutoProperties,");

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]) + 8, Indent(lines[head + 1]));
        Assert.Equal("Feature.LineContinuation,", lines[head + 1].TrimStart());

        var body = Array.FindIndex(lines, head, l => l.Trim() == "Return True");
        Assert.True(body >= 0);
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[body]));
    }

    [Fact]
    public void KeepsAnAuthorWrittenColonInASelectCase()
    {
        var lines = Lines("SelectCaseInline");

        Assert.Contains(lines, l => l.Trim() == "Case 0 : Return \"\"");
        Assert.Contains(lines, l => l.Trim() == "Case 3 : Return \"            \"");
        Assert.Contains(lines, l => l.Trim() == "Case Else : Return \"other\"");
    }

    [Fact]
    public void DoesNotJoinACaseWrittenOverTwoLines()
    {
        var lines = Lines("SelectCaseInline");

        var head = Array.FindIndex(lines, l => l.Trim() == "Case 0");
        Assert.True(head >= 0);
        Assert.Equal("Return \"\"", lines[head + 1].Trim());
    }

    [Fact]
    public void WrapsAnOverlongInlineCaseToItsOwnLine()
    {
        var lines = Lines("SelectCaseInline");

        Assert.Contains(
            lines,
            l => l.TrimStart().StartsWith("Return \"SomeVery", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            lines,
            l => l.Contains(" : Return \"SomeVery", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void DoesNotWrapAFittingLineForATrailingComment()
    {
        var lines = Lines("TrailingComments");

        Assert.Contains(
            lines,
            l => l.Trim() == "For index = 0 To elements.GetLength(0) - 1 ' walk every row"
        );
        Assert.Contains(
            lines,
            l => l.Trim() == "Dim upper = elements.GetLength(0) - 1 ' the last index"
        );
        Assert.Contains(lines, l => l.Trim() == "Dim both = first AndAlso second ' both of them");
        Assert.DoesNotContain(lines, l => l.TrimEnd().EndsWith("-", StringComparison.Ordinal));
    }

    [Fact]
    public void StillBreaksWhereCodeFollowsTheComment()
    {
        var lines = Lines("TrailingComments");

        Assert.Contains(lines, l => l.Trim() == "alpha, ' the first term");
        Assert.Contains(lines, l => l.Trim() == "beta");
    }

    /// <summary>
    /// The formatter owns the spacing between tokens rather than copying the author's. No golden
    /// fixture carries badly-spaced input, because there used to be a pre-pass that fixed it before
    /// the rules ran; these are the cases that pre-pass covered.
    /// </summary>
    [Theory]
    [InlineData("Dim x As Integer=1", "Dim x As Integer = 1")]
    [InlineData("Dim w= 1+2", "Dim w = 1 + 2")]
    [InlineData("Dim y = Foo( a ,b )", "Dim y = Foo(a, b)")]
    [InlineData("Dim z = a\\b", "Dim z = a \\ b")]
    [InlineData("Call  obj . Method ( x )", "Call obj.Method(x)")]
    [InlineData("Dim q = If( a>0 , a , -a )", "Dim q = If(a > 0, a, -a)")]
    [InlineData("Dim c = CType( x , Long )", "Dim c = CType(x, Long)")]
    [InlineData("Dim n As Integer?=Nothing", "Dim n As Integer? = Nothing")]
    [InlineData("Dim a = New With {Key .Id = 1}", "Dim a = New With {Key .Id = 1}")]
    [InlineData("Dim a = New With {Key.Id = 1}", "Dim a = New With {Key .Id = 1}")]
    [InlineData("Dim v = obj?.Name", "Dim v = obj?.Name")]
    public void NormalizesSpacingBetweenTokens(string statement, string expected)
    {
        var source =
            $"Module M\r\n    Sub S()\r\n        {statement}\r\n    End Sub\r\nEnd Module\r\n";

        var result = VbFormatter.Format(source);

        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));
        Assert.Contains(
            expected,
            result.Text.ReplaceLineEndings("\n").Split('\n').Select(l => l.Trim())
        );
    }

    [Fact]
    public void DoubleIndentsAWrappedHandlesClauseBelowTheParameterList()
    {
        var lines = Lines("HandlesClause");

        var head = Array.FindIndex(lines, l => l.Trim() == ") Handles A________.Click,");

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]) + 8, Indent(lines[head + 1]));
        Assert.Equal("B_________.Click,", lines[head + 1].TrimStart());
    }

    [Fact]
    public void ForcesTheParameterListOpenWhenTheHandlesClauseWraps()
    {
        var lines = Lines("HandlesClause");

        var open = Array.FindIndex(lines, l => l.Trim() == "Private Sub Handler(");
        Assert.True(open >= 0);
        Assert.Equal("sender As Object, e As EventArgs", lines[open + 1].Trim());
    }

    [Fact]
    public void LeavesTheParameterListClosedWhenTheHandlesClauseFitsFlat()
    {
        var lines = Lines("HandlesClause");

        Assert.Contains(
            lines,
            l =>
                l.Trim()
                == "Private Sub ShortHandler(sender As Object, e As EventArgs) Handles A________.Click, B_________.Click"
        );
    }

    [Fact]
    public void SingleIndentsAWrappedClassImplementsStatement()
    {
        var lines = Lines("ImplementsClause");

        var head = Array.FindIndex(
            lines,
            l => l.Trim() == "Implements InterfaceA__________________,"
        );

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]) + 4, Indent(lines[head + 1]));
        Assert.Equal("InterfaceB__________________,", lines[head + 1].TrimStart());
    }

    [Fact]
    public void DoubleIndentsAWrappedMethodImplementsClause()
    {
        var lines = Lines("ImplementsClause");

        var head = Array.FindIndex(
            lines,
            l => l.Trim() == ") As Integer Implements IComparer______________.Compare,"
        );

        Assert.True(head >= 0);
        Assert.Equal(Indent(lines[head]) + 8, Indent(lines[head + 1]));
    }

    [Fact]
    public void KeepsTheTypeParametersFlatWhileTheParametersBreak()
    {
        var lines = Lines("GenericSignature");

        Assert.Contains(
            lines,
            l =>
                l.Trim()
                == "Public Function BreaksParametersBeforeTypeParameters(Of TValue, CValue, KValue)("
        );
    }

    [Fact]
    public void KeepsAGenericReturnTypeFlatOnceTheParametersBreak()
    {
        var lines = Lines("GenericSignature");

        var open = Array.FindIndex(
            lines,
            l => l.Trim() == "Public Function BreaksParametersBeforeGenericReturnType("
        );

        Assert.True(open >= 0);
        Assert.Equal("paramA As Integer, paramB As Integer", lines[open + 1].Trim());
        Assert.Equal(
            ") As Tuple(Of LongTypeName___________, LongTypeName___________, LongTypeName___________)",
            lines[open + 2].Trim()
        );
    }

    [Fact]
    public void BreaksATupleReturnTypeOneElementPerLine()
    {
        var lines = Lines("GenericSignature");

        var open = Array.FindIndex(
            lines,
            l => l.Trim() == "Public Function BreaksTupleReturnTypeOneElementPerLine("
        );

        Assert.True(open >= 0);
        Assert.Equal("ParamA As VeryLongTypeName________________________,", lines[open + 3].Trim());
        Assert.Equal(")", lines[open + 6].Trim());
    }

    [Fact]
    public void LeavesAnIgnoredNodeAsWritten()
    {
        const string Source = """
            Module M

              ' vbfmt-ignore
              Public Sub Weird(      )
                      Dim x =     1
              End Sub

            End Module

            """;

        var result = VbFormatter.Format(Source.ReplaceLineEndings("\r\n"));
        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics));

        var lines = result.Text.ReplaceLineEndings("\n").Split('\n');

        Assert.Contains(lines, l => l == "    ' vbfmt-ignore");
        Assert.Contains(lines, l => l == "    Public Sub Weird(      )");
        Assert.Contains(lines, l => l == "          Dim x =     1");
        Assert.Contains(lines, l => l == "  End Sub");
    }

    [Fact]
    public void IgnoresOnlyTheNodeTheMarkerPrecedes()
    {
        const string Source = """
            Module M

                Public Sub S()
                    ' vbfmt-ignore
                    Dim   a    =    1
                    Dim   b    =    2
                End Sub

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(lines, l => l == "        Dim   a    =    1");
        Assert.Contains(lines, l => l == "        Dim b = 2");
    }

    [Fact]
    public void DoesNotIgnoreTheWholeFileForAMarkerOnTheFirstStatement()
    {
        const string Source = """
            ' vbfmt-ignore
            Option Strict    On

            Module M
                Public Sub S(   )
                    Dim   a   =   1
                End Sub
            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(lines, l => l == "Option Strict    On");
        Assert.Contains(lines, l => l == "    Public Sub S()");
        Assert.Contains(lines, l => l == "        Dim a = 1");
    }

    [Fact]
    public void RecognizesTheMarkerWithRemAndAnyCase()
    {
        const string Source = """
            Module M

                Public Sub S()
                    REM VBFMT-IGNORE
                    Dim   a    =    1
                End Sub

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(lines, l => l == "        REM VBFMT-IGNORE");
        Assert.Contains(lines, l => l == "        Dim   a    =    1");
    }

    [Fact]
    public void DoesNotTreatASimilarCommentAsTheMarker()
    {
        const string Source = """
            Module M

                Public Sub S()
                    ' vbfmt-ignored
                    Dim   a    =    1
                    ' see vbfmt-ignore
                    Dim   b    =    2
                End Sub

            End Module

            """;

        var lines = VbFormatter
            .Format(Source.ReplaceLineEndings("\r\n"))
            .Text.ReplaceLineEndings("\n")
            .Split('\n');

        Assert.Contains(lines, l => l == "        Dim a = 1");
        Assert.Contains(lines, l => l == "        Dim b = 2");
    }

    private static string[] Lines(string name) =>
        VbFormatter.Format(TestCases.ReadInput(name)).Text.ReplaceLineEndings("\n").Split('\n');

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    private static CompilationUnitSyntax Parse(string source) =>
        (CompilationUnitSyntax)VisualBasicSyntaxTree.ParseText(source).GetRoot();

    private static List<string> ImportClauses(CompilationUnitSyntax root) =>
        root
            .Imports.SelectMany(i => i.ImportsClauses)
            .Select(c => c.ToString().Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
