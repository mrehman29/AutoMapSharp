using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Configuration;

namespace AutoMapSharp.Execution;

/// <summary>
/// Maps between objects and string-keyed dictionaries, including <see cref="ExpandoObject"/>.
/// </summary>
internal static class DictionaryMapping
{
    private static readonly MethodInfo TryGetMethod = typeof(DictionaryMapping).GetMethod(nameof(TryGetValue), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ConvertMethod = typeof(DictionaryMapping).GetMethod(nameof(ConvertValue), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo SetMethod = typeof(IDictionary<string, object?>).GetProperty("Item")!.GetSetMethod()!;

    /// <summary>
    /// True for <c>IDictionary&lt;string, object&gt;</c>, <c>IReadOnlyDictionary&lt;string, object&gt;</c> and types that implement them, such as <see cref="ExpandoObject"/>.
    /// </summary>
    public static bool IsStringObjectDictionary(Type type) =>
        Implements(type, typeof(IDictionary<string, object>)) || Implements(type, typeof(IReadOnlyDictionary<string, object>));

    private static bool Implements(Type type, Type contract) => contract.IsAssignableFrom(type);

    /// <summary>
    /// Builds <c>dictionary -&gt; object</c>: each destination member reads the key with its name, ignoring case.
    /// Missing keys leave the member alone.
    /// </summary>
    public static Expression BuildFromDictionary(
        MapEngine engine,
        TypeMappingDescriptor? map,
        Expression source,
        Type destinationType,
        Expression destinationIn,
        Expression useDestination,
        Expression context)
    {
        var parameterless = destinationType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        Expression create = parameterless != null ? Expression.New(parameterless)
            : destinationType.IsValueType ? Expression.New(destinationType)
            : throw new Exceptions.AutoMapSharpConfigurationException($"Type '{destinationType.FullName}' needs a parameterless constructor to be mapped from a dictionary.");

        var destination = Expression.Variable(destinationType, "d");
        var value = Expression.Variable(typeof(object), "value");
        var body = new List<Expression>
        {
            Expression.Assign(destination, destinationType.IsValueType
                ? Expression.Condition(useDestination, destinationIn, create)
                : Expression.Condition(Expression.AndAlso(useDestination, Expression.NotEqual(destinationIn, Expression.Constant(null, destinationType))), destinationIn, create))
        };

        var sourceObject = Expression.Convert(source, typeof(object));
        foreach (var member in SourceMemberMatcher.GetDestinationMembers(destinationType, map))
        {
            PropertyRule? rule = null;
            map?.PropertyRules.TryGetValue(member.Name, out rule);
            if (rule?.IsIgnored == true) continue;

            var memberType = SourceMemberMatcher.MemberType(member);
            var target = Expression.MakeMemberAccess(destination, member);
            if (rule?.HasConstantValue == true)
            {
                body.Add(Expression.Assign(target, Expression.Convert(Expression.Constant(rule.ConstantValue), memberType)));
                continue;
            }

            var key = rule?.SourceMemberName ?? member.Name;
            body.Add(Expression.IfThen(
                Expression.Call(TryGetMethod, sourceObject, Expression.Constant(key), value),
                Expression.Assign(target, Expression.Call(ConvertMethod.MakeGenericMethod(memberType), Expression.Constant(engine), value, context))));
        }

        body.Add(destination);
        return Expression.Block(destinationType, new[] { destination, value }, body);
    }

    /// <summary>
    /// Builds <c>object -&gt; dictionary</c>: every readable source member becomes a key.
    /// </summary>
    public static Expression BuildToDictionary(TypeMappingDescriptor? map, Expression source, Type destinationType, Expression destinationIn, Expression useDestination)
    {
        Expression create = destinationType == typeof(ExpandoObject) || destinationType.IsAssignableFrom(typeof(Dictionary<string, object?>)) && destinationType.IsInterface
            ? destinationType == typeof(ExpandoObject) ? Expression.New(typeof(ExpandoObject)) : Expression.New(typeof(Dictionary<string, object?>))
            : Expression.New(destinationType);

        var dictionary = Expression.Variable(typeof(IDictionary<string, object?>), "dictionary");
        var body = new List<Expression>
        {
            Expression.Assign(dictionary, Expression.Convert(
                Expression.Condition(Expression.AndAlso(useDestination, Expression.NotEqual(destinationIn, Expression.Constant(null, destinationType))), destinationIn, Expression.Convert(create, destinationType)),
                typeof(IDictionary<string, object?>)))
        };

        foreach (var member in SourceMemberMatcher.GetSourceMembers(source.Type, map))
        {
            body.Add(Expression.Call(dictionary, SetMethod, Expression.Constant(member.Name), Expression.Convert(Expression.MakeMemberAccess(source, member), typeof(object))));
        }

        body.Add(Expression.Convert(dictionary, destinationType));
        return Expression.Block(destinationType, new[] { dictionary }, body);
    }

    private static bool TryGetValue(object dictionary, string key, out object? value)
    {
        switch (dictionary)
        {
            case IDictionary<string, object?> mutable:
                if (mutable.TryGetValue(key, out value)) return true;
                foreach (var pair in mutable)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }

                break;

            case IReadOnlyDictionary<string, object?> readOnly:
                if (readOnly.TryGetValue(key, out value)) return true;
                foreach (var pair in readOnly)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }

                break;
        }

        value = null;
        return false;
    }

    private static T ConvertValue<T>(MapEngine engine, object? value, ResolutionContext context)
    {
        if (value is null) return default!;
        if (value is T typed) return typed;
        if (BuiltInConversions.IsSimple(typeof(T))) return BuiltInConversions.RuntimeConvert<T>(value);
        return (T)engine.GetHolder(value.GetType(), typeof(T)).MapObject(value, null, false, context)!;
    }
}
