using System;
using System.Collections.Generic;
using System.Reflection;

namespace AutoMapSharp.Flattening;

/// <summary>
/// Represents a step in a flattened member navigation path.
/// </summary>
public sealed class PathStep
{
    public MemberInfo Member { get; }
    public Type ReturnType { get; }

    public PathStep(MemberInfo member, Type returnType)
    {
        Member = member;
        ReturnType = returnType;
    }

    public object? GetValue(object target)
    {
        if (Member is PropertyInfo prop)
        {
            return prop.GetValue(target);
        }
        if (Member is MethodInfo method)
        {
            return method.Invoke(target, Array.Empty<object>());
        }
        if (Member is FieldInfo field)
        {
            return field.GetValue(target);
        }
        return null;
    }
}

/// <summary>
/// Resolves deep member chains on a source object matching flattened destination property names.
/// </summary>
public static class FlatteningResolver
{
    /// <summary>
    /// Attempts to find a navigation path on <paramref name="sourceType"/> that matches <paramref name="destinationMemberName"/>.
    /// </summary>
    public static List<PathStep>? ResolvePath(Type sourceType, string destinationMemberName, IEnumerable<string>? prefixes = null)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (string.IsNullOrEmpty(destinationMemberName)) return null;

        var path = new List<PathStep>();
        if (TryResolveRecursive(sourceType, destinationMemberName, path, prefixes))
        {
            return path;
        }

        return null;
    }

    private static bool TryResolveRecursive(
        Type currentType,
        string remainingName,
        List<PathStep> currentPath,
        IEnumerable<string>? prefixes)
    {
        if (string.IsNullOrEmpty(remainingName))
        {
            return currentPath.Count > 0;
        }

        var members = GetCandidateMembers(currentType, prefixes);

        var candidates = new List<(string Name, MemberInfo Member, Type ReturnType)>();

        foreach (var m in members)
        {
            if (remainingName.StartsWith(m.Name, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(m);
            }
        }

        // Longest name first (greedy match).
        candidates.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));

        foreach (var candidate in candidates)
        {
            var nextRemaining = remainingName.Substring(candidate.Name.Length);
            var step = new PathStep(candidate.Member, candidate.ReturnType);
            currentPath.Add(step);

            if (string.IsNullOrEmpty(nextRemaining))
            {
                return true;
            }

            if (TryResolveRecursive(candidate.ReturnType, nextRemaining, currentPath, prefixes))
            {
                return true;
            }

            currentPath.RemoveAt(currentPath.Count - 1);
        }

        return false;
    }

    private static List<(string Name, MemberInfo Member, Type ReturnType)> GetCandidateMembers(
        Type type,
        IEnumerable<string>? prefixes)
    {
        var result = new List<(string Name, MemberInfo Member, Type ReturnType)>();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.CanRead)
            {
                result.Add((prop.Name, prop, prop.PropertyType));
            }
        }

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            if (IsMappableMethod(method))
            {
                string name = method.Name;
                if (prefixes != null)
                {
                    foreach (var prefix in prefixes)
                    {
                        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length)
                        {
                            result.Add((name.Substring(prefix.Length), method, method.ReturnType));
                        }
                    }
                }
                result.Add((name, method, method.ReturnType));
            }
        }

        return result;
    }

    /// <summary>
    /// A source method can act as a member only when it is a plain, parameterless instance method that returns a value.
    /// Methods from <see cref="object"/>, including overrides such as a record's <c>GetHashCode</c>, are excluded,
    /// so a destination property named <c>Type</c> or <c>HashCode</c> never reads <c>GetType()</c> or <c>GetHashCode()</c>.
    /// </summary>
    internal static bool IsMappableMethod(MethodInfo method)
    {
        if (method.ReturnType == typeof(void) || method.GetParameters().Length != 0)
        {
            return false;
        }

        if (method.IsStatic || method.IsSpecialName || method.IsGenericMethodDefinition || method.Name.IndexOf('<') >= 0)
        {
            return false;
        }

        return method.GetBaseDefinition().DeclaringType != typeof(object);
    }
}
