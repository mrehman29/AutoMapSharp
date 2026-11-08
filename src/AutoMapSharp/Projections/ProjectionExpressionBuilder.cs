using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutoMapSharp.Collections;
using AutoMapSharp.Configuration;
using AutoMapSharp.Conversions;
using AutoMapSharp.Execution;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Flattening;

namespace AutoMapSharp.Projections;

/// <summary>
/// Dynamically constructs and caches LINQ expression trees (<see cref="Expression{TDelegate}"/>)
/// for database projection queries via Entity Framework Core / LINQ providers.
/// </summary>
public static class ProjectionExpressionBuilder
{
    // One cache per engine, so two engines with different configuration never share projections.
    // The cache holds only the expression shape. Parameter values are bound per call and never cached.
    private static readonly ConditionalWeakTable<IMapEngine, ConcurrentDictionary<string, LambdaExpression>> _projectionCaches = new();

    /// <summary>
    /// Gets or creates a cached projection expression from <paramref name="sourceType"/> to <paramref name="destinationType"/>.
    /// </summary>
    public static LambdaExpression GetOrCreateProjection(
        Type sourceType,
        Type destinationType,
        IMapEngine engine,
        object? parameters = null,
        string[]? membersToExpand = null)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));
        if (engine == null) throw new ArgumentNullException(nameof(engine));

        var paramDict = ExtractParameters(parameters);
        var expandList = membersToExpand ?? Array.Empty<string>();
        var cacheKey = BuildCacheKey(sourceType, destinationType, expandList);
        // Scoped views of one engine share its configuration, so they share one projection cache too.
        var owner = engine is MapEngine mapEngine ? mapEngine.Root : engine;
        var cache = _projectionCaches.GetValue(owner, _ => new ConcurrentDictionary<string, LambdaExpression>(StringComparer.Ordinal));

        var shape = cache.GetOrAdd(cacheKey, _ =>
        {
            var sourceParam = Expression.Parameter(sourceType, "src");
            var body = BuildMemberInit(sourceType, destinationType, sourceParam, engine, expandList, null, string.Empty, 0);
            return Expression.Lambda(body, sourceParam);
        });

        if (paramDict == null || paramDict.Count == 0)
        {
            return shape;
        }

        return (LambdaExpression)new ClosureParameterBinder(paramDict).Visit(shape);
    }

    private static Expression BuildMemberInit(
        Type sourceType,
        Type destinationType,
        Expression sourceExpression,
        IMapEngine engine,
        string[] membersToExpand,
        IDictionary<string, object?>? parameters,
        string pathPrefix,
        int depth)
    {
        return new ProjectionPlanner(engine, membersToExpand).Build(sourceType, destinationType, sourceExpression, pathPrefix, depth);
    }

    /// <summary>
    /// Builds the query expression for one projection. Only expressions a LINQ provider can translate are produced:
    /// member reads, conversions, constructors, null checks, type checks and Select/ToList for collections.
    /// Configuration that needs .NET code at run time, such as resolvers, converters and conditions, raises a clear
    /// error instead of being ignored.
    /// </summary>
    private sealed class ProjectionPlanner
    {
        private const int MaxNesting = 10;
        private readonly IMapEngine _engine;
        private readonly string[] _membersToExpand;
        private readonly Dictionary<(Type, Type), int> _visits = new();

        public ProjectionPlanner(IMapEngine engine, string[] membersToExpand)
        {
            _engine = engine;
            _membersToExpand = membersToExpand;
        }

        public Expression Build(Type sourceType, Type destinationType, Expression source, string path, int depth)
        {
            if (depth > MaxNesting)
            {
                return Expression.Constant(null, destinationType);
            }

            var map = _engine.FindDescriptor(sourceType, destinationType);
            var key = (sourceType, destinationType);
            _visits.TryGetValue(key, out var visits);
            if (map?.MaxDepth is int maxDepth && visits >= maxDepth)
            {
                return Expression.Constant(null, destinationType);
            }

            _visits[key] = visits + 1;
            try
            {
                if (map != null && (map.CustomConverter != null || map.CustomConverterType != null))
                {
                    throw NotTranslatable(sourceType, destinationType, null, "a type converter (ConvertUsing)");
                }

                if (map?.AsType is { } asType)
                {
                    return Expression.Convert(Build(sourceType, asType, source, path, depth), destinationType);
                }

                var derived = DerivedPairs(map, sourceType, destinationType);
                if (derived.Count == 0 && !destinationType.IsAbstract && !destinationType.IsInterface)
                {
                    return BuildObject(sourceType, destinationType, source, map, path, depth);
                }

                // Inheritance: a chain of type checks, most derived first, which EF Core translates for TPH models.
                Expression result = destinationType.IsAbstract || destinationType.IsInterface
                    ? Expression.Constant(null, destinationType)
                    : BuildObject(sourceType, destinationType, source, map, path, depth);

                foreach (var (derivedSource, derivedDestination) in derived.OrderBy(d => Depth(d.Source)))
                {
                    var derivedInit = Build(derivedSource, derivedDestination, Expression.Convert(source, derivedSource), path, depth + 1);
                    result = Expression.Condition(Expression.TypeIs(source, derivedSource), Expression.Convert(derivedInit, destinationType), result);
                }

                return result;
            }
            finally
            {
                _visits[key] = visits;
            }
        }

        private List<(Type Source, Type Destination)> DerivedPairs(TypeMappingDescriptor? map, Type sourceType, Type destinationType)
        {
            var pairs = new List<(Type, Type)>(map?.IncludedDerivedTypes ?? Array.Empty<(Type, Type)>());
            if (map?.IncludeAllDerivedTypes == true && _engine is MapEngine mapEngine)
            {
                pairs.AddRange(mapEngine.Configuration.Maps
                    .Where(m => m.SourceType != sourceType && sourceType.IsAssignableFrom(m.SourceType) &&
                                destinationType.IsAssignableFrom(m.DestinationType) && !m.DestinationType.IsAbstract)
                    .Select(m => (m.SourceType, m.DestinationType))
                    .Where(p => !pairs.Contains(p)));
            }

            return pairs;
        }

        private static int Depth(Type type)
        {
            var depth = 0;
            for (var current = type.BaseType; current != null; current = current.BaseType) depth++;
            return depth;
        }

        private Expression BuildObject(Type sourceType, Type destinationType, Expression source, TypeMappingDescriptor? map, string path, int depth)
        {
            var bindings = new List<MemberAssignment>();

            foreach (var member in SourceMemberMatcher.GetDestinationMembers(destinationType, map))
            {
                PropertyRule? rule = null;
                map?.PropertyRules.TryGetValue(member.Name, out rule);
                if (rule?.IsIgnored == true)
                {
                    continue;
                }

                var memberPath = string.IsNullOrEmpty(path) ? member.Name : path + "." + member.Name;
                if (rule?.ExplicitExpansion == true &&
                    !_membersToExpand.Any(m => string.Equals(m, member.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(m, memberPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (rule?.CustomResolverType != null) throw NotTranslatable(sourceType, destinationType, member.Name, "a resolver (MapWith)");
                if (rule?.ValueConverterType != null) throw NotTranslatable(sourceType, destinationType, member.Name, "a value converter (ConvertUsing)");
                if (rule?.Condition != null || rule?.ValueCondition != null) throw NotTranslatable(sourceType, destinationType, member.Name, "a condition (If or PreCondition)");

                var memberType = SourceMemberMatcher.MemberType(member);
                var value = ReadValue(sourceType, source, member.Name, memberType, rule, map, memberPath, depth);
                if (value == null)
                {
                    continue;
                }

                bindings.Add(Expression.Bind(member, value));
            }

            return Construct(destinationType, bindings);
        }

        private Expression? ReadValue(Type sourceType, Expression source, string memberName, Type memberType, PropertyRule? rule, TypeMappingDescriptor? map, string memberPath, int depth)
        {
            Expression? raw;
            if (rule?.HasConstantValue == true)
            {
                return Expression.Constant(rule.ConstantValue, memberType);
            }

            if (rule?.SourceExpression != null)
            {
                raw = ParameterReplacer.Replace(rule.SourceExpression.Body, rule.SourceExpression.Parameters[0],
                    source.Type == rule.SourceExpression.Parameters[0].Type ? source : Expression.Convert(source, rule.SourceExpression.Parameters[0].Type));
            }
            else if (rule?.SourceMemberName != null)
            {
                raw = Expression.PropertyOrField(source, rule.SourceMemberName);
            }
            else
            {
                var access = SourceMemberMatcher.Find(sourceType, memberName, map);
                raw = access == null ? null : access.Steps.Aggregate(source, (current, step) => step switch
                {
                    PropertyInfo p => Expression.Property(current, p),
                    FieldInfo f => Expression.Field(current, f),
                    MethodInfo { IsStatic: true } m => Expression.Call(m, current),
                    MethodInfo m => Expression.Call(current, m),
                    _ => current
                });
            }

            if (raw == null)
            {
                return rule?.HasFallbackValue == true ? Expression.Constant(rule.FallbackValue, memberType) : null;
            }

            // Fallback becomes a COALESCE in SQL.
            if (rule?.HasFallbackValue == true && (!raw.Type.IsValueType || Nullable.GetUnderlyingType(raw.Type) != null))
            {
                var underlying = Nullable.GetUnderlyingType(raw.Type) ?? raw.Type;
                if (rule.FallbackValue == null || underlying.IsInstanceOfType(rule.FallbackValue))
                {
                    raw = Expression.Coalesce(raw, Expression.Constant(rule.FallbackValue, underlying));
                }
            }

            return Convert(raw, memberType, memberName, memberPath, depth);
        }

        private Expression Convert(Expression value, Type targetType, string memberName, string memberPath, int depth)
        {
            if (targetType == value.Type)
            {
                return value;
            }

            if (CollectionPlanner.IsCollection(targetType) && CollectionPlanner.IsEnumerable(value.Type))
            {
                var sourceElement = CollectionPlanner.GetElementType(value.Type)!;
                var targetElement = CollectionPlanner.GetElementType(targetType)!;
                var item = Expression.Parameter(sourceElement, "item");
                var elementBody = Convert(item, targetElement, memberName, memberPath, depth);
                Expression items = value;
                if (elementBody != item)
                {
                    var select = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
                        .MakeGenericMethod(sourceElement, targetElement);
                    items = Expression.Call(select, value, Expression.Lambda(elementBody, item));
                }

                if (targetType.IsArray)
                {
                    return Expression.Call(typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!.MakeGenericMethod(targetElement), items);
                }

                if (targetType.IsAssignableFrom(typeof(List<>).MakeGenericType(targetElement)))
                {
                    return Expression.Convert(Expression.Call(typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!.MakeGenericMethod(targetElement), items), targetType);
                }

                throw new AutoMapSharpConfigurationException(
                    $"Member '{memberName}' has collection type '{targetType.Name}', which cannot be built in a query. Use List<T>, T[] or a collection interface.");
            }

            if (targetType.IsAssignableFrom(value.Type))
            {
                return Expression.Convert(value, targetType);
            }

            if (targetType == typeof(string))
            {
                return Expression.Call(value, value.Type.GetMethod(nameof(ToString), Type.EmptyTypes) ?? typeof(object).GetMethod(nameof(ToString))!);
            }

            var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (!BuiltInConversions.IsSimple(underlyingTarget) && !CollectionPlanner.IsEnumerable(underlyingTarget))
            {
                var sourceType = Nullable.GetUnderlyingType(value.Type) ?? value.Type;
                var nested = Build(sourceType, targetType, value, memberPath, depth + 1);
                return value.Type.IsValueType
                    ? nested
                    : Expression.Condition(Expression.Equal(value, Expression.Constant(null, value.Type)), Expression.Constant(null, targetType), nested);
            }

            try
            {
                return Expression.Convert(value, targetType);
            }
            catch (InvalidOperationException)
            {
                throw new AutoMapSharpConfigurationException(
                    $"Member '{memberName}' cannot be converted from '{value.Type.Name}' to '{targetType.Name}' in a query. Use MapSource with an expression the database can run.");
            }
        }

        private static Expression Construct(Type destinationType, List<MemberAssignment> bindings)
        {
            var parameterless = destinationType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (parameterless != null || destinationType.IsValueType)
            {
                var create = parameterless != null ? Expression.New(parameterless) : Expression.New(destinationType);
                return Expression.MemberInit(create, bindings);
            }

            // Records and immutable types: constructor parameters take matching bindings, the rest are member-initialized.
            var ctor = destinationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault()
                ?? throw new AutoMapSharpConfigurationException($"Type '{destinationType.FullName}' has no public constructor that a query can call.");

            var used = new HashSet<MemberAssignment>();
            var arguments = ctor.GetParameters().Select(parameter =>
            {
                var binding = bindings.FirstOrDefault(b => string.Equals(b.Member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (binding == null)
                {
                    return (Expression)Expression.Default(parameter.ParameterType);
                }

                used.Add(binding);
                return binding.Expression.Type == parameter.ParameterType ? binding.Expression : Expression.Convert(binding.Expression, parameter.ParameterType);
            }).ToArray();

            var remaining = bindings.Where(b => !used.Contains(b) && b.Member is PropertyInfo { CanWrite: true } or FieldInfo).ToList();
            var created = Expression.New(ctor, arguments);
            return remaining.Count == 0 ? created : Expression.MemberInit(created, remaining);
        }

        private static AutoMapSharpConfigurationException NotTranslatable(Type sourceType, Type destinationType, string? member, string what)
        {
            var where = member == null ? "The map" : $"Member '{member}' on the map";
            return new AutoMapSharpConfigurationException(
                $"{where} from '{sourceType.Name}' to '{destinationType.Name}' uses {what}, which runs .NET code and cannot be translated to a query. " +
                "Use MapSource with an expression the database can run, Skip the member for this projection, or map in memory after the query.");
        }
    }

    private static Type? GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            return collectionType.GetGenericArguments()[0];
        }

        var ienum = collectionType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return ienum?.GetGenericArguments()[0];
    }

    private static IDictionary<string, object?>? ExtractParameters(object? parameters)
    {
        if (parameters == null) return null;
        if (parameters is IDictionary<string, object?> dict) return dict;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            result[prop.Name] = prop.GetValue(parameters);
        }
        return result;
    }

    private static string BuildCacheKey(Type src, Type dest, string[] membersToExpand)
    {
        var expandKey = string.Join(",", membersToExpand.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        return $"{src.AssemblyQualifiedName}->{dest.AssemblyQualifiedName};Expand=[{expandKey}]";
    }

    /// <summary>
    /// Binds projection parameter values to the variables a module captured in its MapSource lambdas.
    /// </summary>
    /// <remarks>
    /// A module declares a parameter by capturing a local or field, for example
    /// <c>int userId = 0; ... MapSource(s => s.Items.Count(i => i.UserId == userId))</c>.
    /// The compiler turns that capture into a member access on a constant closure object.
    /// Only those accesses are rebound. Member accesses on the entity, such as <c>i.UserId</c>,
    /// start from a lambda parameter and are never touched, whatever their name.
    /// Each value is read from a fresh holder object instead of being embedded as a constant,
    /// so EF Core sends it as a SQL parameter and can reuse the query plan.
    /// </remarks>
    private sealed class ClosureParameterBinder : ExpressionVisitor
    {
        private readonly IDictionary<string, object?> _parameters;

        public ClosureParameterBinder(IDictionary<string, object?> parameters)
        {
            _parameters = parameters;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression &&
                node.Member is FieldInfo or PropertyInfo &&
                TryGetParameter(node.Member.Name, out var value))
            {
                return CreateParameterAccess(node.Member.Name, node.Type, value);
            }

            return base.VisitMember(node);
        }

        private bool TryGetParameter(string name, out object? value)
        {
            if (_parameters.TryGetValue(name, out value))
            {
                return true;
            }

            foreach (var kvp in _parameters)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static Expression CreateParameterAccess(string name, Type type, object? value)
        {
            if (!TypeConversionEngine.TryConvert(value, type, out var converted))
            {
                throw new ArgumentException(
                    $"Projection parameter '{name}' has a value of type '{value?.GetType().FullName}', which cannot be converted to '{type.FullName}'.",
                    nameof(value));
            }

            var holderType = typeof(ProjectionParameter<>).MakeGenericType(type);
            var holder = Activator.CreateInstance(holderType)!;
            holderType.GetField(nameof(ProjectionParameter<object>.Value))!.SetValue(holder, converted);
            return Expression.Field(Expression.Constant(holder), nameof(ProjectionParameter<object>.Value));
        }
    }
}

/// <summary>
/// Holds one projection parameter value for one query.
/// </summary>
internal sealed class ProjectionParameter<T>
{
    public T Value = default!;
}
