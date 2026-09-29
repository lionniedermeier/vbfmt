using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VisualBasicFormatter.Language.Expressions;
using VisualBasicFormatter.Language.Statements;
using VisualBasicFormatter.Language.Xml;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language;

/// <summary>
/// The constructs that may be broken across lines. Everything here goes through
/// <see cref="FormatContext.BreakAfter"/>, which refuses any token VB does not continue implicitly.
/// </summary>
internal sealed partial class VbDocVisitor
{
    /// <summary>Dots the chain rule decided to break at, looked up again on the way down.</summary>
    private readonly HashSet<SyntaxToken> _chainBreaks = [];

    private bool _insideLastResortDot;

    /// <inheritdoc/>
    public override Doc VisitArgumentList(ArgumentListSyntax node) =>
        VbDocBuilder.List(
            node.OpenParenToken,
            node.Arguments,
            node.CloseParenToken,
            ListLayout.Packed,
            this,
            _context
        );

    /// <inheritdoc/>
    public override Doc VisitParameterList(ParameterListSyntax node) =>
        node.Parent is LambdaHeaderSyntax { Parent: SingleLineLambdaExpressionSyntax }
            ? StructuralFallback.Format(node, this, _context)
            : VbDocBuilder.List(
                node.OpenParenToken,
                node.Parameters,
                node.CloseParenToken,
                ListLayout.Packed,
                this,
                _context
            );

    /// <summary>Rare and short, so a line of its own per type parameter costs nothing.</summary>
    public override Doc VisitTypeParameterList(TypeParameterListSyntax node) =>
        VbDocBuilder.List(
            node.OpenParenToken,
            Doc.Concat(_context.Token(node.OfKeyword), Doc.Space),
            node.Parameters,
            node.CloseParenToken,
            ListLayout.OnePerLine,
            this,
            _context
        );

    /// <inheritdoc cref="VisitTypeParameterList"/>
    public override Doc VisitTypeArgumentList(TypeArgumentListSyntax node) =>
        VbDocBuilder.List(
            node.OpenParenToken,
            Doc.Concat(_context.Token(node.OfKeyword), Doc.Space),
            node.Arguments,
            node.CloseParenToken,
            ListLayout.OnePerLine,
            this,
            _context
        );

    public override Doc VisitTupleType(TupleTypeSyntax node) =>
        VbDocBuilder.List(
            node.OpenParenToken,
            node.Elements,
            node.CloseParenToken,
            ListLayout.OnePerLine,
            this,
            _context
        );

    /// <inheritdoc cref="VisitTypeParameterList"/>
    public override Doc VisitAttributeList(AttributeListSyntax node) =>
        VbDocBuilder.List(
            node.LessThanToken,
            node.Attributes,
            node.GreaterThanToken,
            ListLayout.OnePerLine,
            this,
            _context
        );

    /// <summary>
    /// The braces of an array literal, a collection initializer or a <c>From</c> clause. One element
    /// per line: an added element is then a one-line diff rather than a reflow of the whole literal.
    /// </summary>
    public override Doc VisitCollectionInitializer(CollectionInitializerSyntax node) =>
        VbDocBuilder.List(
            node.OpenBraceToken,
            node.Initializers,
            node.CloseBraceToken,
            ListLayout.OnePerLine,
            this,
            _context
        );

    /// <summary>One member per line, so the initializer reads as the list of assignments it is.</summary>
    public override Doc VisitObjectMemberInitializer(ObjectMemberInitializerSyntax node) =>
        Doc.Concat(
            _context.Token(node.WithKeyword),
            Doc.Space,
            VbDocBuilder.List(
                node.OpenBraceToken,
                node.Initializers,
                node.CloseBraceToken,
                ListLayout.OnePerLine,
                this,
                _context
            )
        );

    /// <summary>
    /// <c>If(condition, whenTrue, whenFalse)</c>. Its commas are children of the expression rather
    /// than a list of their own, but they are the same continuation point an argument list breaks
    /// at, so it is laid out as one.
    /// </summary>
    public override Doc VisitTernaryConditionalExpression(
        TernaryConditionalExpressionSyntax node
    ) =>
        Conditional(
            node.IfKeyword,
            node.OpenParenToken,
            [node.Condition, node.WhenTrue, node.WhenFalse],
            [node.FirstCommaToken, node.SecondCommaToken],
            node.CloseParenToken
        );

    /// <summary>The two-argument <c>If(value, fallback)</c>.</summary>
    public override Doc VisitBinaryConditionalExpression(BinaryConditionalExpressionSyntax node) =>
        Conditional(
            node.IfKeyword,
            node.OpenParenToken,
            [node.FirstExpression, node.SecondExpression],
            [node.CommaToken],
            node.CloseParenToken
        );

    private Doc Conditional(
        SyntaxToken ifKeyword,
        SyntaxToken open,
        ImmutableArray<ExpressionSyntax> operands,
        ImmutableArray<SyntaxToken> commas,
        SyntaxToken close
    )
    {
        var elements = new Doc[operands.Length];
        var hasBlock = false;

        for (var i = 0; i < operands.Length; i++)
        {
            elements[i] = Format(operands[i]);

            if (TrailingExpansion.IsBlock(operands[i]))
            {
                hasBlock = true;
            }
        }

        return Doc.Concat(
            _context.Token(ifKeyword),
            VbDocBuilder.List(
                open,
                Doc.Nothing,
                ImmutableCollectionsMarshal.AsImmutableArray(elements),
                commas,
                close,
                TrailingExpansion.IsExpandable(operands[^1]),
                hasBlock,
                ListLayout.Packed,
                _context
            )
        );
    }

    /// <summary>
    /// The outermost call of a chain owns the chain: it marks the dots to break at and wraps the
    /// whole run, so the links below it only have to honour the marks.
    /// </summary>
    public override Doc VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var dots = MemberChainRule.BreakDots(node, _context);

        if (dots.IsEmpty)
        {
            if (!_insideLastResortDot && MemberChainRule.HasLastResortDot(node, _context))
            {
                var wasInside = _insideLastResortDot;
                _insideLastResortDot = true;
                var fill = MemberChainRule.LastResortDot(node, this, _context);
                _insideLastResortDot = wasInside;

                return fill;
            }

            return StructuralFallback.Format(node, this, _context);
        }

        foreach (var dot in dots)
        {
            _chainBreaks.Add(dot);
        }

        var run = VbDocBuilder.Run(StructuralFallback.Format(node, this, _context));

        return BlockHeader.Wrap(node, run, run);
    }

    /// <summary>
    /// A link of a chain the rule above marked. Every other member access is left to the structural
    /// fallback, so that a plain property hop keeps whatever break opportunities that offers.
    /// </summary>
    public override Doc VisitMemberAccessExpression(MemberAccessExpressionSyntax node) =>
        _chainBreaks.Contains(node.OperatorToken)
            ? Doc.Concat(
                Format(node.Expression),
                _context.Token(node.OperatorToken),
                _context.SoftBreakAfter(node.OperatorToken),
                Format(node.Name)
            )
            : StructuralFallback.Format(node, this, _context);

    /// <inheritdoc/>
    public override Doc VisitQueryExpression(QueryExpressionSyntax node) =>
        QueryExpressionRule.Format(node, this, _context);

    public override Doc VisitParenthesizedExpression(ParenthesizedExpressionSyntax node) =>
        ParenthesizedConditionRule.Format(node, this, _context)
        ?? StructuralFallback.Format(node, this, _context);

    public override Doc VisitEqualsValue(EqualsValueSyntax node) =>
        AssignmentTail(node.EqualsToken, node.Value)
        ?? StructuralFallback.Format(node, this, _context);

    public override Doc VisitAssignmentStatement(AssignmentStatementSyntax node) =>
        !_context.MustPrintVerbatim(node)
        && AssignmentTail(node.OperatorToken, node.Right) is { } tail
            ? Doc.Concat(Format(node.Left), Doc.Space, tail)
            : StructuralFallback.Format(node, this, _context);

    public override Doc VisitSelectClause(SelectClauseSyntax node) =>
        SelectClauseRule.Format(node, this, _context);

    /// <inheritdoc cref="JoinClauseRule"/>
    public override Doc VisitSimpleJoinClause(SimpleJoinClauseSyntax node) =>
        JoinClauseRule.Format(node, this, _context);

    /// <inheritdoc cref="JoinClauseRule"/>
    public override Doc VisitGroupJoinClause(GroupJoinClauseSyntax node) =>
        JoinClauseRule.Format(node, this, _context);

    /// <inheritdoc/>
    public override Doc VisitBinaryExpression(BinaryExpressionSyntax node) =>
        BinaryExpressionRule.IsRunHead(node, _context)
            ? BinaryExpressionRule.Format(node, this, _context)
            : StructuralFallback.Format(node, this, _context);

    private Doc? AssignmentTail(SyntaxToken op, ExpressionSyntax? value) =>
        QueryAssignmentRule.Tail(op, value, this, _context)
        ?? XmlAssignmentRule.Tail(op, value, this, _context)
        ?? ConcatAssignmentRule.Tail(op, value, this, _context)
        ?? LambdaAssignmentRule.Tail(op, value, this, _context);
}
