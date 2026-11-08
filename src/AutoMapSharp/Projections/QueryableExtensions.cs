using System;
using System.Linq;
using System.Linq.Expressions;
using AutoMapSharp.Projections;

namespace AutoMapSharp;

/// <summary>
/// LINQ projection extensions for querying databases via ORMs (Entity Framework Core, Linq2Db, etc.).
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Projects an <see cref="IQueryable"/> to an <see cref="IQueryable{TDest}"/> using registered mapping rules.
    /// Generates a SQL <c>SELECT</c> statement through Entity Framework Core without retrieving unneeded columns.
    /// </summary>
    /// <typeparam name="TDest">The target destination DTO type.</typeparam>
    /// <param name="source">The source queryable (e.g. DbSet&lt;T&gt;).</param>
    /// <param name="engine">The mapping engine instance containing configuration metadata.</param>
    /// <param name="parameters">Optional anonymous object containing query parameter values.</param>
    /// <param name="membersToExpand">Optional member names to explicitly expand for navigation properties.</param>
    /// <returns>An <see cref="IQueryable{TDest}"/> executing as a projected SQL query.</returns>
    public static IQueryable<TDest> ProjectOnto<TDest>(
        this IQueryable source,
        IMapEngine engine,
        object? parameters = null,
        params string[] membersToExpand)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (engine == null) throw new ArgumentNullException(nameof(engine));

        var sourceType = source.ElementType;
        var destType = typeof(TDest);

        var projectionLambda = ProjectionExpressionBuilder.GetOrCreateProjection(
            sourceType, destType, engine, parameters, membersToExpand);

        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(sourceType, destType);

        var selectCall = Expression.Call(null, selectMethod, source.Expression, Expression.Quote(projectionLambda));
        return source.Provider.CreateQuery<TDest>(selectCall);
    }

    /// <summary>
    /// Projects with explicitly expanded members given as expressions, for example
    /// <c>ProjectOnto&lt;OrderDto&gt;(engine, null, d =&gt; d.Items, d =&gt; d.Customer.Address)</c>.
    /// </summary>
    public static IQueryable<TDest> ProjectOnto<TDest>(
        this IQueryable source,
        IMapEngine engine,
        object? parameters,
        Expression<Func<TDest, object?>> memberToExpand,
        params Expression<Func<TDest, object?>>[] moreMembersToExpand)
    {
        if (memberToExpand == null) throw new ArgumentNullException(nameof(memberToExpand));

        var paths = new[] { memberToExpand }.Concat(moreMembersToExpand ?? Array.Empty<Expression<Func<TDest, object?>>>())
            .Select(ToMemberPath)
            .ToArray();
        return source.ProjectOnto<TDest>(engine, parameters, paths);
    }

    private static string ToMemberPath<TDest>(Expression<Func<TDest, object?>> expression)
    {
        var names = new System.Collections.Generic.List<string>();
        var body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : expression.Body;
        while (body is MemberExpression member)
        {
            names.Insert(0, member.Member.Name);
            body = member.Expression!;
        }

        if (body is not ParameterExpression || names.Count == 0)
        {
            throw new ArgumentException($"Expansion '{expression}' must be a member chain such as d => d.Items or d => d.Customer.Address.", nameof(expression));
        }

        return string.Join(".", names);
    }
}
