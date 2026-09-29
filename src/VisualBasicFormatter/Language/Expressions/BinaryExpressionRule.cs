using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VisualBasicFormatter.Language.Statements;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language.Expressions;

/// <summary>
/// A run of equally ranked binary operators, broken after every operator or after none. Mixing
/// ranks in one run would suggest a precedence the code does not have, so a run stops where the
/// operator changes.
/// </summary>
internal static class BinaryExpressionRule
{
    /// <summary>Whether <paramref name="node"/> starts a run rather than continuing one.</summary>
    public static bool IsRunHead(BinaryExpressionSyntax node, FormatContext context) =>
        ContinuationPoints.IsBreakableOperator(node.OperatorToken, context.Unbreakable)
        && !(
            node.Parent is BinaryExpressionSyntax parent
            && parent.Left == node
            && parent.OperatorToken.IsKind(node.OperatorToken.Kind())
        );

    /// <summary>
    /// Prints the run headed by <paramref name="node"/>: head, then operator and operand.
    /// </summary>
    /// <remarks>
    /// <paramref name="isNested"/> is whether this run is itself an operand of another run -- a
    /// higher-precedence chain feeding into a lower-precedence one, e.g. <c>AndAlso</c> inside
    /// <c>OrElse</c>. A nested run must not contribute its own indent on top of the outer one; see
    /// <see cref="VbDocBuilder.Run(ImmutableArray{Doc}, bool)"/>.
    /// </remarks>
    public static Doc Format(
        BinaryExpressionSyntax node,
        VbDocVisitor visitor,
        FormatContext context,
        bool isNested = false
    )
    {
        var count = 1;
        for (
            var scan = node;
            scan.Left is BinaryExpressionSyntax scanLeft
                && scanLeft.OperatorToken.IsKind(node.OperatorToken.Kind());
            scan = scanLeft
        )
        {
            count++;
        }

        var operators = new SyntaxToken[count];
        var operands = new ExpressionSyntax[count + 1];
        var index = count - 1;
        var current = node;

        while (true)
        {
            operators[index] = current.OperatorToken;
            operands[index + 1] = current.Right;

            if (
                current.Left is BinaryExpressionSyntax left
                && left.OperatorToken.IsKind(node.OperatorToken.Kind())
            )
            {
                current = left;
                index--;
                continue;
            }

            operands[0] = current.Left;
            break;
        }

        var preserved = false;
        if (node.OperatorToken.IsKind(SyntaxKind.AmpersandToken))
        {
            foreach (var op in operators)
            {
                if (context.EndsItsLine(op))
                {
                    preserved = true;
                    break;
                }
            }
        }

        // Content and separator in turn: an operand carries the operator that follows it, and the
        // break that operator permits stands between the two.
        var items = ImmutableArray.CreateBuilder<Doc>();

        for (var i = 0; i < operands.Length; i++)
        {
            if (i >= operators.Length)
            {
                items.Add(FormatOperand(operands[i], visitor, context));
                continue;
            }

            items.Add(
                Doc.Concat(
                    FormatOperand(operands[i], visitor, context),
                    Doc.Space,
                    context.Token(operators[i])
                )
            );
            items.Add(Separator(operators[i], preserved, context));
        }

        var all = items.DrainToImmutable();
        var run = VbDocBuilder.Run(all, indent: !isNested);

        if (isNested)
        {
            return run;
        }

        var single = Doc.Group(
            Doc.Concat(all[0], Doc.Indent(Doc.Concat(all.RemoveAt(0))))
        );

        return BlockHeader.Wrap(node, run, single);
    }

    private static Doc Separator(SyntaxToken op, bool preserved, FormatContext context)
    {
        if (!preserved)
        {
            return context.BreakAfter(op);
        }

        return context.EndsItsLine(op) ? context.HardBreakAfter(op) : Doc.Space;
    }

    /// <summary>
    /// Formats one operand. An operand that is itself the head of a differently ranked run is
    /// recursed into directly, marked as nested, instead of being handed to the generic visitor --
    /// going through <see cref="VbDocVisitor.VisitBinaryExpression"/> would produce a fresh,
    /// un-nested call and so a second, additive indent.
    /// </summary>
    private static Doc FormatOperand(
        ExpressionSyntax operand,
        VbDocVisitor visitor,
        FormatContext context
    ) =>
        operand is BinaryExpressionSyntax binary && IsRunHead(binary, context)
            ? Format(binary, visitor, context, isNested: true)
            : visitor.Format(operand);
}
