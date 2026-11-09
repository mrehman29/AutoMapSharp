using System.Collections.Generic;
using System.Linq.Expressions;

namespace AutoMapSharp.Execution;

/// <summary>
/// Makes member chains in a <c>MapSource</c> expression null-safe, so <c>s => s.Customer.Address.City</c>
/// yields null when <c>Customer</c> or <c>Address</c> is null instead of throwing.
/// </summary>
/// <remarks>
/// Only member reads and instance method calls on intermediate objects are guarded. Nested lambdas, such as the
/// predicate in <c>s.Items.Count(i =&gt; i.Active)</c>, are left untouched, and exceptions thrown by user code
/// still propagate.
/// </remarks>
internal sealed class NullSafeRewriter : ExpressionVisitor
{
    public static Expression Rewrite(LambdaExpression lambda) => new NullSafeRewriter().Visit(lambda.Body)!;

    protected override Expression VisitLambda<T>(Expression<T> node) => node;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression == null)
        {
            return node;
        }

        var instance = Visit(node.Expression)!;
        if (!NeedsGuard(node.Expression, instance))
        {
            return node.Update(instance);
        }

        var temp = Expression.Variable(instance.Type, "value");
        return Guard(temp, instance, node.Update(temp));
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Object == null || node.Type == typeof(void))
        {
            return base.VisitMethodCall(node);
        }

        var instance = Visit(node.Object)!;
        var arguments = Visit(node.Arguments);
        if (!NeedsGuard(node.Object, instance))
        {
            return node.Update(instance, arguments);
        }

        var temp = Expression.Variable(instance.Type, "value");
        return Guard(temp, instance, node.Update(temp, arguments));
    }

    private static bool NeedsGuard(Expression original, Expression visited) =>
        !visited.Type.IsValueType && original is not ParameterExpression && original is not ConstantExpression;

    private static Expression Guard(ParameterExpression temp, Expression instance, Expression access) =>
        Expression.Block(
            access.Type,
            new[] { temp },
            Expression.Assign(temp, instance),
            Expression.Condition(Expression.Equal(temp, Expression.Constant(null, temp.Type)), Expression.Default(access.Type), access));
}
