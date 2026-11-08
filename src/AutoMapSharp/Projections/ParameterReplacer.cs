using System;
using System.Linq.Expressions;

namespace AutoMapSharp.Projections;

/// <summary>
/// Replaces occurrences of a specific <see cref="ParameterExpression"/> with a replacement expression.
/// </summary>
internal sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _target;
    private readonly Expression _replacement;

    public ParameterReplacer(ParameterExpression target, Expression replacement)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _target ? _replacement : base.VisitParameter(node);
    }

    public static Expression Replace(Expression expression, ParameterExpression target, Expression replacement)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        return new ParameterReplacer(target, replacement).Visit(expression);
    }
}
