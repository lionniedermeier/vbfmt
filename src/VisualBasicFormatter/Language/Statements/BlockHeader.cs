using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language.Statements;

internal static class BlockHeader
{
    public static bool IsHeaderExpression(SyntaxNode node)
    {
        var current = node;

        while (current.Parent is UnaryExpressionSyntax unary && unary.Operand == current)
        {
            current = unary;
        }

        return current.Parent switch
        {
            IfStatementSyntax parent => parent.Condition == current,
            ElseIfStatementSyntax parent => parent.Condition == current,
            WhileStatementSyntax parent => parent.Condition == current,
            WhileOrUntilClauseSyntax parent => parent.Condition == current,
            ForEachStatementSyntax parent => parent.Expression == current,
            _ => false,
        };
    }

    public static bool ClosesHeader(SyntaxToken close)
    {
        for (var node = close.Parent; node is not null; node = node.Parent)
        {
            if (node.GetLastToken() != close)
            {
                return false;
            }

            if (node is ExpressionSyntax && IsHeaderExpression(node))
            {
                return true;
            }
        }

        return false;
    }

    public static Doc Wrap(SyntaxNode node, Doc doubled, Doc single)
    {
        if (!IsHeaderExpression(node))
        {
            return doubled;
        }

        return node.GetLastToken().Kind() is SyntaxKind.CloseParenToken or SyntaxKind.CloseBraceToken
            ? Doc.RootChoice(Doc.Indent(doubled), single)
            : Doc.Indent(doubled);
    }
}
