using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VisualBasicFormatter.Language.Statements;
using VisualBasicFormatter.Printing;

namespace VisualBasicFormatter.Language.Expressions;

/// <summary>
/// Finds the dots an invocation chain may be broken after. VB continues implicitly behind a member
/// qualifier, so <c>Values.</c> at the end of a line needs no underscore -- which is why this
/// formatter breaks <em>after</em> the dot and never before it.
/// </summary>
internal static class MemberChainRule
{
    /// <summary>
    /// The dots to break at, in source order, or empty when <paramref name="node"/> does not head a
    /// chain worth breaking.
    /// </summary>
    public static ImmutableArray<SyntaxToken> BreakDots(
        InvocationExpressionSyntax node,
        FormatContext context
    )
    {
        // Only the outermost call owns the chain; otherwise every link would offer it again.
        if (ContinuesUpwards(node))
        {
            return [];
        }

        var count = CountDots(node, context);

        // One dot is not a chain; a single call is better served by breaking its argument list.
        // Plain property hops are skipped above: splitting State.Gesellschaften.Values shortens the
        // line barely and reads badly.
        if (count < 2)
        {
            return [];
        }

        var dots = new SyntaxToken[count];
        var index = count - 1;

        for (ExpressionSyntax? current = node; current is not null; )
        {
            switch (current)
            {
                case MemberAccessExpressionSyntax access when access.Expression is not null:
                    if (
                        IsInvoked(access)
                        && ContinuationPoints.IsImplicitAfter(
                            access.OperatorToken,
                            context.Unbreakable
                        )
                    )
                    {
                        dots[index--] = access.OperatorToken;
                    }

                    current = access.Expression;
                    break;

                case InvocationExpressionSyntax invocation:
                    current = invocation.Expression;
                    break;

                default:
                    current = null;
                    break;
            }
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(dots);
    }

    private static int CountDots(InvocationExpressionSyntax node, FormatContext context)
    {
        var count = 0;

        for (ExpressionSyntax? current = node; current is not null; )
        {
            switch (current)
            {
                // A missing Expression is the leading dot of a With block, of an initializer key or
                // of a conditional access -- the cases where VB does demand an underscore.
                case MemberAccessExpressionSyntax access when access.Expression is not null:
                    if (
                        IsInvoked(access)
                        && ContinuationPoints.IsImplicitAfter(
                            access.OperatorToken,
                            context.Unbreakable
                        )
                    )
                    {
                        count++;
                    }

                    current = access.Expression;
                    break;

                case InvocationExpressionSyntax invocation:
                    current = invocation.Expression;
                    break;

                default:
                    current = null;
                    break;
            }
        }

        return count;
    }

    private static bool IsInvoked(MemberAccessExpressionSyntax access) =>
        access.Parent is InvocationExpressionSyntax invocation && invocation.Expression == access;

    private static bool ContinuesUpwards(InvocationExpressionSyntax node) =>
        node.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == node,
            InvocationExpressionSyntax invocation => invocation.Expression == node,
            _ => false,
        };

    public static bool HasLastResortDot(InvocationExpressionSyntax node, FormatContext context) =>
        !ContinuesUpwards(node)
        && !context.MustPrintVerbatim(node)
        && node.Expression is MemberAccessExpressionSyntax { Expression: not null } access
        && ContinuationPoints.IsImplicitAfter(access.OperatorToken, context.Unbreakable)
        && CountDots(node, context) == 1;

    public static Doc LastResortDot(
        InvocationExpressionSyntax node,
        VbDocVisitor visitor,
        FormatContext context
    )
    {
        var access = (MemberAccessExpressionSyntax)node.Expression!;
        var dot = access.OperatorToken;
        var head = access.Expression!;

        var tail = node.ArgumentList is null
            ? visitor.Format(access.Name)
            : Doc.Concat(
                visitor.Format(access.Name),
                context.Gap(access.Name, node.ArgumentList),
                visitor.Format(node.ArgumentList)
            );

        var headDoc = visitor.Format(head);

        if (headDoc.Expands || tail.Expands)
        {
            var fallback = Doc.Fill([
                Doc.Concat(headDoc, context.Token(dot)),
                Doc.Indent(context.SoftBreakAfter(dot)),
                tail,
            ]);

            return BlockHeader.IsHeaderExpression(node) ? Doc.Indent(fallback) : fallback;
        }

        var states = ImmutableArray.CreateBuilder<Doc>(4);
        states.Add(Doc.Concat(headDoc, context.Token(dot), tail));

        if (Doc.ForceBreak(tail) is { } argsBroken)
        {
            states.Add(Doc.Concat(headDoc, context.Token(dot), argsBroken));
        }

        if (Doc.ForceBreak(headDoc) is { } headBroken)
        {
            states.Add(Doc.Concat(headBroken, context.Token(dot), tail));
        }

        var dotBroken = DotBroken(head, dot, headDoc, tail, visitor, context);

        states.Add(BlockHeader.IsHeaderExpression(node) ? Doc.Indent(dotBroken) : dotBroken);

        return Doc.ConditionalGroup(states.DrainToImmutable());
    }

    private static Doc DotBroken(
        ExpressionSyntax head,
        SyntaxToken dot,
        Doc headDoc,
        Doc tail,
        VbDocVisitor visitor,
        FormatContext context
    )
    {
        if (TryGetHops(head, context, out var root, out var hops))
        {
            using var parts = new DocListBuilder(2 * (hops.Length + 1) + 1);

            parts.Add(Doc.Concat(visitor.Format(root), context.Token(hops[0].Dot)));
            parts.Add(context.SoftBreakAfter(hops[0].Dot));

            for (var i = 1; i < hops.Length; i++)
            {
                parts.Add(Doc.Concat(visitor.Format(hops[i - 1].Name), context.Token(hops[i].Dot)));
                parts.Add(context.SoftBreakAfter(hops[i].Dot));
            }

            parts.Add(Doc.Concat(visitor.Format(hops[^1].Name), context.Token(dot)));
            parts.Add(context.SoftBreakAfter(dot));
            parts.Add(tail);

            return Doc.Indent(Doc.Fill(parts.ToImmutable(), strict: true));
        }

        return Doc.Indent(
            Doc.Fill(
                [Doc.Concat(headDoc, context.Token(dot)), context.SoftBreakAfter(dot), tail],
                strict: true
            )
        );
    }

    private static bool TryGetHops(
        ExpressionSyntax head,
        FormatContext context,
        out ExpressionSyntax root,
        out ImmutableArray<(SyntaxToken Dot, IdentifierNameSyntax Name)> hops
    )
    {
        var found = new List<(SyntaxToken Dot, IdentifierNameSyntax Name)>();
        var current = head;

        while (
            current is MemberAccessExpressionSyntax { Expression: not null } access
            && access.Name is IdentifierNameSyntax name
            && ContinuationPoints.IsImplicitAfter(access.OperatorToken, context.Unbreakable)
        )
        {
            found.Add((access.OperatorToken, name));
            current = access.Expression;
        }

        found.Reverse();

        if (
            found.Count == 0
            || current
                is not (
                    IdentifierNameSyntax
                    or MeExpressionSyntax
                    or MyBaseExpressionSyntax
                    or MyClassExpressionSyntax
                )
        )
        {
            root = head;
            hops = [];
            return false;
        }

        root = current;
        hops = ImmutableCollectionsMarshal.AsImmutableArray(found.ToArray());
        return true;
    }
}
