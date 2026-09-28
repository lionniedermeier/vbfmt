using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VisualBasicFormatter.Language;

namespace VisualBasicFormatter.Tests;

/// <summary>
/// The block rules against constructs the golden fixtures never reach. Roslyn's own normalization is
/// the oracle: as long as the rules only decide indentation and blank lines, the two have to agree.
/// </summary>
public sealed class BlockSkeletonTests
{
    /// <summary>Every block kind VB has, nested, with comments in the places that lose them.</summary>
    private const string EveryBlockKind = """
        ' File header.

        Imports System

        Namespace Contoso.Sample
            ''' <summary>Does things.</summary>
            ''' <remarks>And documents them.</remarks>
            Public Class Worker
                Inherits WorkerBase
                Implements IWorker

                Private ReadOnly mItems As New List(Of String)()

                Public Enum Mode
                    Fast
                    Slow
                End Enum

                Public Event Finished As EventHandler

                Public Sub New(ByVal seed As Integer)
                    mSeed = seed
                End Sub

                Public Property Count As Integer
                    Get
                        Return mItems.Count
                    End Get
                    Set(value As Integer)
                        mCount = value
                    End Set
                End Property

                ' Runs everything.
                Public Sub Run(ByVal mode As Mode)
                    If mode = Mode.Fast Then
                        Fire()
                    ElseIf mode = Mode.Slow Then
                        Wait()
                    Else
                        Idle()
                    End If

                    For index = 0 To 10
                        Step1(index)
                    Next

                    For Each item In mItems
                        Step2(item)
                    Next

                    While mBusy
                        Spin()
                    End While

                    Do
                        Tick()
                    Loop While mBusy

                    Try
                        Risky()
                    Catch ex As InvalidOperationException
                        Report(ex)
                    Catch ex As Exception
                        Report(ex)
                    Finally
                        Cleanup()
                    End Try

                    Using scope = New Scope()
                        scope.Enter()
                    End Using

                    With mTarget
                        .Reset()
                    End With

                    SyncLock mGate
                        mCounter += 1
                    End SyncLock

                    Select Case mode
                        Case Mode.Fast
                            Fire()
                        Case Mode.Slow
                            Wait()
                        Case Else
                            Idle()
                    End Select
                End Sub

                Public Function Describe() As String ' trailing comment
                    Return "worker"
                End Function
            End Class

            Public Structure Point
                Public X As Integer
            End Structure

            Public Interface IWorker
                Sub Run()
            End Interface

            Public Module Helpers
                Public Sub Nudge()
                End Sub
            End Module

        End Namespace
        """;

    [Fact]
    public void IndentsEveryBlockKindLikeRoslyn()
    {
        var (before, after) = Format(EveryBlockKind);

        // The closing line break is ours; the sample literal carries none.
        Assert.Equal(before.ReplaceLineEndings("\n") + "\n", after.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void LosesNoComment()
    {
        var (before, after) = Format(EveryBlockKind);

        Assert.Equal(Comments(before), Comments(after));
    }

    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void LosesNoCommentInTheFixturesEither(string name)
    {
        var (before, after) = Format(TestCases.ReadInput(name));

        Assert.Equal(Comments(before), Comments(after));
    }

    /// <summary>
    /// The strongest structural check there is: the same tokens, in the same order. It catches a
    /// dropped or duplicated node that <c>IsEquivalentTo</c> would only report as "the code changed".
    /// </summary>
    [Theory]
    [MemberData(nameof(TestCases.Names), MemberType = typeof(TestCases))]
    public void EmitsTheSameTokensInTheSameOrder(string name)
    {
        var (before, after) = Format(TestCases.ReadInput(name));

        Assert.Equal(Tokens(before), Tokens(after));
    }

    [Fact]
    public void EmitsTheSameTokensForEveryBlockKindToo()
    {
        var (before, after) = Format(EveryBlockKind);

        Assert.Equal(Tokens(before), Tokens(after));
    }

    /// <summary>Blank lines mark a break in the reading flow; more than one adds nothing.</summary>
    [Fact]
    public void CollapsesMultipleBlankLinesIntoOne()
    {
        const string Source =
            "Module M\r\n    Sub S()\r\n        Dim a = 1\r\n\r\n\r\n\r\n        Dim b = 2\r\n    End Sub\r\nEnd Module\r\n";

        var (_, after) = Format(Source);

        Assert.Equal(
            "Module M\r\n    Sub S()\r\n        Dim a = 1\r\n\r\n        Dim b = 2\r\n    End Sub\r\nEnd Module\r\n",
            after
        );
    }

    /// <summary>
    /// A colon joins two statements that the tree already holds separately, so giving each its own
    /// line changes nothing but the layout.
    /// </summary>
    [Fact]
    public void SplitsColonChainedStatements()
    {
        const string Source =
            "Module M\r\n    Sub S()\r\n        Dim a = 1 : Dim b = 2\r\n    End Sub\r\nEnd Module\r\n";

        var (_, after) = Format(Source);

        Assert.Contains(
            "        Dim a = 1\r\n        Dim b = 2\r\n",
            after,
            StringComparison.Ordinal
        );
    }

    /// <summary>A comment behind the last declaration hangs on the end-of-file token.</summary>
    [Fact]
    public void KeepsACommentAtEndOfFile()
    {
        const string Source = "Module M\r\nEnd Module\r\n\r\n' The end.\r\n";

        var (_, after) = Format(Source);

        Assert.EndsWith("' The end.\r\n", after, StringComparison.Ordinal);
    }

    [Fact]
    public void AddsATrailingNewlineAtEndOfFile()
    {
        var (_, after) = Format("Module M\r\nEnd Module");

        Assert.EndsWith("End Module\r\n", after, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses, then runs the rule engine. The first item is the parsed tree as text -- the same
    /// tokens and comments the engine sees, for the multiset comparisons below to check against.
    /// </summary>
    private static (string Parsed, string Formatted) Format(string source)
    {
        var options = new FormatterOptions();
        var newLine = VbFormatter.DetectNewLine(source);

        var tree = VisualBasicSyntaxTree.ParseText(
            source,
            new VisualBasicParseOptions(options.LanguageVersion)
        );
        Assert.DoesNotContain(tree.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

        var root = (CompilationUnitSyntax)tree.GetRoot();

        return (root.ToFullString(), DocEngine.Format(root, options, newLine));
    }

    private static List<string> Comments(string source) =>
        VisualBasicSyntaxTree
            .ParseText(source)
            .GetRoot()
            .DescendantTrivia(descendIntoTrivia: true)
            .Where(t =>
                t.IsKind(SyntaxKind.CommentTrivia)
                || t.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia)
            )
            .Select(t => t.ToString().Trim())
            .ToList();

    private static List<string> Tokens(string source) =>
        VisualBasicSyntaxTree
            .ParseText(source)
            .GetRoot()
            .DescendantTokens()
            .Where(t => t.Span.Length > 0)
            .Select(t => t.Text)
            .ToList();
}
