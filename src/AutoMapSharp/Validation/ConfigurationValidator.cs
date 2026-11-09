using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Configuration;
using AutoMapSharp.Execution;

namespace AutoMapSharp.Validation;

/// <summary>
/// Checks a frozen configuration. It uses the same member matching and conversion rules as the engine,
/// so a map that validates is a map the engine can run.
/// </summary>
internal sealed class ConfigurationValidator
{
    private readonly MapEngine _engine;
    private readonly List<ConfigurationError> _errors = new();
    private readonly HashSet<(Type, Type)> _visited = new();

    public ConfigurationValidator(MapEngine engine)
    {
        _engine = engine;
    }

    public ConfigurationValidationResult Validate()
    {
        foreach (var map in _engine.Configuration.Maps)
        {
            ValidateMap(map.SourceType, map.DestinationType, map, context: null);
        }

        return new ConfigurationValidationResult(_errors);
    }

    private void ValidateMap(Type sourceType, Type destinationType, TypeMappingDescriptor? map, string? context)
    {
        if (!_visited.Add((sourceType, destinationType)))
        {
            return;
        }

        var unmapped = new List<string>();
        var unusedSource = new List<string>();
        var problems = new List<string>();
        var nested = new List<(Type Source, Type Destination, string Context)>();

        // A type converter owns the whole destination. Dictionary keys are only known at run time.
        if (map != null && (map.CustomConverter != null || map.CustomConverterType != null) ||
            DictionaryMapping.IsStringObjectDictionary(sourceType) || DictionaryMapping.IsStringObjectDictionary(destinationType))
        {
            return;
        }

        if (map != null)
        {
            foreach (var (derivedSource, derivedDestination) in map.IncludedDerivedTypes)
            {
                if (!_engine.Configuration.Contains(derivedSource, derivedDestination))
                {
                    problems.Add($"Include<{derivedSource.Name}, {derivedDestination.Name}>() refers to a pair that has no map.");
                }
            }
        }

        var consumedSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var constructorMembers = ValidateConstruction(sourceType, destinationType, map, problems, consumedSource);
        var memberList = map?.MemberListToValidate ?? MemberList.Destination;
        var pathRoots = new HashSet<string>(map?.PathRules.Select(p => p.Path[0].Name) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var path in map?.PathRules ?? Array.Empty<PathRule>())
        {
            AddConsumed(path.Rule.SourceExpression, consumedSource);
        }

        foreach (var member in SourceMemberMatcher.GetDestinationMembers(destinationType, map))
        {
            if (constructorMembers.Contains(member.Name))
            {
                continue;
            }

            var memberType = SourceMemberMatcher.MemberType(member);
            PropertyRule? rule = null;
            map?.PropertyRules.TryGetValue(member.Name, out rule);

            if (rule != null)
            {
                if (rule.IsIgnored || rule.HasConstantValue || rule.CustomResolverType != null)
                {
                    continue;
                }

                if (rule.SourceExpression != null)
                {
                    AddConsumed(rule.SourceExpression, consumedSource);
                    if (rule.ValueConverterType == null)
                    {
                        CheckConversion(rule.SourceExpression.ReturnType, memberType, member.Name, problems, nested);
                    }

                    continue;
                }

                if (rule.SourceMemberName != null)
                {
                    consumedSource.Add(rule.SourceMemberName);
                    continue;
                }
            }

            if (pathRoots.Contains(member.Name))
            {
                continue;
            }

            var access = SourceMemberMatcher.Find(sourceType, member.Name, map);
            if (access != null)
            {
                consumedSource.Add(access.Steps[0].Name);
                if (rule?.ValueConverterType == null)
                {
                    CheckConversion(access.Type, memberType, member.Name, problems, nested);
                }

                continue;
            }

            if (rule?.HasFallbackValue == true)
            {
                continue;
            }

            unmapped.Add(member.Name);
        }

        if (memberList == MemberList.Source)
        {
            foreach (var sourceMember in SourceMemberMatcher.GetSourceMembers(sourceType, map))
            {
                if (!consumedSource.Contains(sourceMember.Name) && map?.UnvalidatedSourceMembers.Contains(sourceMember.Name) != true)
                {
                    unusedSource.Add(sourceMember.Name);
                }
            }
        }

        if (memberList != MemberList.Destination)
        {
            unmapped.Clear();
        }

        if (memberList == MemberList.None)
        {
            unusedSource.Clear();
        }

        if (unmapped.Count > 0 || unusedSource.Count > 0 || problems.Count > 0)
        {
            _errors.Add(new ConfigurationError(sourceType, destinationType, unmapped, unusedSource, problems, context));
        }

        foreach (var (nestedSource, nestedDestination, nestedContext) in nested)
        {
            var nestedMap = _engine.Configuration.Find(nestedSource, nestedDestination);
            if (nestedMap == null)
            {
                ValidateMap(nestedSource, nestedDestination, null, nestedContext);
            }
        }
    }

    /// <summary>
    /// Checks the constructor the engine would use and returns the members it sets.
    /// </summary>
    private static HashSet<string> ValidateConstruction(Type sourceType, Type destinationType, TypeMappingDescriptor? map, List<string> problems, HashSet<string> consumedSource)
    {
        var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var constructors = destinationType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (map != null)
        {
            foreach (var name in map.CtorParamRules.Keys)
            {
                if (!constructors.Any(c => c.GetParameters().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))))
                {
                    problems.Add($"ForCtorParam(\"{name}\") does not match a parameter of any constructor of {destinationType.Name}.");
                }
            }
        }

        if (map?.ConstructorExpression != null || destinationType.IsAbstract || destinationType.IsInterface || destinationType.IsValueType ||
            constructors.Any(c => c.GetParameters().Length == 0))
        {
            return members;
        }

        var withParameters = constructors.OrderByDescending(c => c.GetParameters().Length).ToList();
        bool Resolvable(ParameterInfo p) =>
            p.Name != null && (map?.CtorParamRules.ContainsKey(p.Name) == true ||
                               (map != null && map.PropertyRules.TryGetValue(p.Name, out var r) && !r.IsIgnored) ||
                               SourceMemberMatcher.Find(sourceType, p.Name, map) != null) || p.HasDefaultValue;

        var chosen = withParameters.FirstOrDefault(c => c.GetParameters().All(Resolvable));
        if (chosen == null)
        {
            if (withParameters.Count > 0)
            {
                var missing = withParameters[0].GetParameters().Where(p => !Resolvable(p)).Select(p => p.Name);
                problems.Add($"No constructor of {destinationType.Name} can be satisfied. Unresolved parameters of the largest one: {string.Join(", ", missing)}. Use ForCtorParam or ConstructUsing.");
            }

            return members;
        }

        foreach (var parameter in chosen.GetParameters())
        {
            if (parameter.Name == null) continue;
            members.Add(parameter.Name);
            var access = SourceMemberMatcher.Find(sourceType, parameter.Name, map);
            if (access != null) consumedSource.Add(access.Steps[0].Name);
        }

        return members;
    }

    private static void AddConsumed(LambdaExpression? expression, HashSet<string> consumed)
    {
        if (expression == null) return;
        new SourceMemberCollector(expression.Parameters[0], consumed).Visit(expression.Body);
    }

    /// <summary>
    /// Mirrors the engine's conversion choice and reports pairs it cannot convert.
    /// </summary>
    private void CheckConversion(Type from, Type to, string memberName, List<string> problems, List<(Type, Type, string)> nested)
    {
        from = Nullable.GetUnderlyingType(from) ?? from;
        var target = Nullable.GetUnderlyingType(to) ?? to;

        if (from == target || target.IsAssignableFrom(from) || from == typeof(object) || _engine.HasExplicitMap(from, target))
        {
            return;
        }

        if (CollectionPlanner.IsCollection(target) && CollectionPlanner.IsEnumerable(from))
        {
            var fromElement = CollectionPlanner.GetElementType(from);
            var toElement = CollectionPlanner.GetElementType(target);
            if (fromElement != null && toElement != null && !CollectionPlanner.TryGetDictionaryTypes(target, out _, out _))
            {
                CheckConversion(fromElement, toElement, memberName + "[]", problems, nested);
            }

            return;
        }

        if (BuiltInConversions.TryBuild(Expression.Parameter(from), target) != null)
        {
            return;
        }

        if (!BuiltInConversions.IsSimple(target))
        {
            nested.Add((from, target, $"Automatic map used by member '{memberName}'."));
            return;
        }

        problems.Add($"Member '{memberName}' cannot be converted from {from.Name} to {target.Name}. Use MapSource, ConvertUsing, or define a map.");
    }

    private sealed class SourceMemberCollector : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly HashSet<string> _consumed;

        public SourceMemberCollector(ParameterExpression parameter, HashSet<string> consumed)
        {
            _parameter = parameter;
            _consumed = consumed;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == _parameter)
            {
                _consumed.Add(node.Member.Name);
            }

            return base.VisitMember(node);
        }
    }
}
