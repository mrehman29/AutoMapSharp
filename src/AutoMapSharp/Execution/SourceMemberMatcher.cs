using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Configuration;
using AutoMapSharp.Conventions;
using AutoMapSharp.Flattening;

namespace AutoMapSharp.Execution;

/// <summary>
/// How a destination member reads its value from the source when no explicit rule is configured.
/// </summary>
internal sealed class SourceAccess
{
    public SourceAccess(IReadOnlyList<MemberInfo> steps, Type type)
    {
        Steps = steps;
        Type = type;
    }

    /// <summary>
    /// One step for a direct member, several for a flattened path such as Customer.Address.City.
    /// </summary>
    public IReadOnlyList<MemberInfo> Steps { get; }

    public Type Type { get; }
}

/// <summary>
/// Finds the source member for a destination member by name, naming convention, prefixes, suffixes and
/// replacements, then flattening. The engine, validation and reverse-map unflattening all use this, so they always agree.
/// </summary>
internal static class SourceMemberMatcher
{
    private static readonly string[] DefaultPrefixes = { "Get" };
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadableCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> DestinationCache = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache = new();

    /// <summary>
    /// Public, readable, non-indexer instance properties.
    /// </summary>
    public static PropertyInfo[] GetReadableProperties(Type type) => ReadableCache.GetOrAdd(type, t =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetGetMethod() != null && p.GetIndexParameters().Length == 0)
            .ToArray());

    /// <summary>
    /// Public, non-indexer instance properties that have a setter of any visibility, such as <c>{ get; private set; }</c> or <c>init</c>.
    /// The returned property is taken from its declaring type, so a private setter declared on a base class is usable.
    /// </summary>
    public static PropertyInfo[] GetDestinationProperties(Type type) => DestinationCache.GetOrAdd(type, t =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.DeclaringType != null && p.DeclaringType != t
                ? p.DeclaringType.GetProperty(p.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) ?? p
                : p)
            .Where(p => p.GetSetMethod(true) != null)
            .ToArray());

    /// <summary>
    /// Public instance fields.
    /// </summary>
    public static FieldInfo[] GetPublicFields(Type type) => FieldCache.GetOrAdd(type, t =>
        t.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsSpecialName).ToArray());

    /// <summary>
    /// The destination members a map writes: settable public properties and writable public fields, filtered by the
    /// module's <c>ShouldMapProperty</c> and <c>ShouldMapField</c>.
    /// </summary>
    public static IReadOnlyList<MemberInfo> GetDestinationMembers(Type type, TypeMappingDescriptor? map)
    {
        var members = new List<MemberInfo>();
        foreach (var property in GetDestinationProperties(type))
        {
            if (map?.ShouldMapProperty == null || map.ShouldMapProperty(property)) members.Add(property);
        }

        foreach (var field in GetPublicFields(type))
        {
            if (!field.IsInitOnly && !field.IsLiteral && (map?.ShouldMapField == null || map.ShouldMapField(field))) members.Add(field);
        }

        return members;
    }

    /// <summary>
    /// The source members a map can read directly: readable public properties and public fields, filtered by the module's hooks.
    /// </summary>
    public static IReadOnlyList<MemberInfo> GetSourceMembers(Type type, TypeMappingDescriptor? map)
    {
        var members = new List<MemberInfo>();
        foreach (var property in GetReadableProperties(type))
        {
            if (map?.ShouldMapProperty == null || map.ShouldMapProperty(property)) members.Add(property);
        }

        foreach (var field in GetPublicFields(type))
        {
            if (map?.ShouldMapField == null || map.ShouldMapField(field)) members.Add(field);
        }

        return members;
    }

    public static Type MemberType(MemberInfo member) => member switch
    {
        PropertyInfo p => p.PropertyType,
        FieldInfo f => f.FieldType,
        MethodInfo m => m.ReturnType,
        _ => throw new NotSupportedException($"Member '{member.Name}' of kind {member.MemberType} is not supported.")
    };

    public static SourceAccess? Find(Type sourceType, string destinationMemberName, TypeMappingDescriptor? map)
    {
        var members = GetSourceMembers(sourceType, map);
        var replacements = (IReadOnlyList<(string, string)>?)map?.MemberNameReplacements ?? Array.Empty<(string, string)>();

        // 1. Same name, ignoring case, after replacements.
        var direct = members.FirstOrDefault(m => string.Equals(Replace(m.Name, replacements), destinationMemberName, StringComparison.OrdinalIgnoreCase));

        // 2. Same words after naming conventions, prefixes, suffixes and replacements, for example first_name -> FirstName.
        if (direct == null)
        {
            var sourceConvention = map?.SourceNamingConvention ?? PascalCaseNamingConvention.Instance;
            var destinationConvention = map?.DestinationNamingConvention ?? PascalCaseNamingConvention.Instance;
            var prefixes = (IReadOnlyList<string>?)map?.RecognizedPrefixes ?? DefaultPrefixes;
            var suffixes = (IReadOnlyList<string>?)map?.RecognizedSuffixes ?? Array.Empty<string>();
            var destinationPrefixes = (IReadOnlyList<string>?)map?.RecognizedDestinationPrefixes ?? Array.Empty<string>();
            var destinationSuffixes = (IReadOnlyList<string>?)map?.RecognizedDestinationSuffixes ?? Array.Empty<string>();
            var destinationWords = destinationConvention.SplitWords(StripPrefixesAndSuffixes(destinationMemberName, destinationPrefixes, destinationSuffixes));

            direct = members.FirstOrDefault(m =>
                WordsMatch(sourceConvention.SplitWords(StripPrefixesAndSuffixes(Replace(m.Name, replacements), prefixes, suffixes)), destinationWords));
        }

        if (direct != null)
        {
            return new SourceAccess(new[] { direct }, MemberType(direct));
        }

        // 3. Extension methods from IncludeSourceExtensionMethods, by name or with a recognized prefix.
        if (map != null && map.SourceExtensionMethodTypes.Count > 0)
        {
            var extension = FindExtensionMethod(sourceType, destinationMemberName, map);
            if (extension != null)
            {
                return new SourceAccess(new MemberInfo[] { extension }, extension.ReturnType);
            }
        }

        // 4. Flattening, for example CustomerAddressCity -> Customer.Address.City, or SpecialNotes -> GetSpecialNotes().
        var path = FlatteningResolver.ResolvePath(sourceType, destinationMemberName, (IEnumerable<string>?)map?.RecognizedPrefixes ?? DefaultPrefixes);
        if (path != null && path.Count > 0 && path.All(step => Allowed(step.Member, map)))
        {
            return new SourceAccess(path.Select(s => s.Member).ToList(), path[path.Count - 1].ReturnType);
        }

        return null;
    }

    private static MethodInfo? FindExtensionMethod(Type sourceType, string destinationMemberName, TypeMappingDescriptor map)
    {
        foreach (var type in map.SourceExtensionMethodTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || method.ReturnType == typeof(void) || method.IsGenericMethodDefinition ||
                    !method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false) ||
                    !parameters[0].ParameterType.IsAssignableFrom(sourceType))
                {
                    continue;
                }

                if (string.Equals(method.Name, destinationMemberName, StringComparison.OrdinalIgnoreCase) ||
                    map.RecognizedPrefixes.Any(prefix => string.Equals(method.Name, prefix + destinationMemberName, StringComparison.OrdinalIgnoreCase)))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static bool Allowed(MemberInfo member, TypeMappingDescriptor? map) => member switch
    {
        PropertyInfo p => map?.ShouldMapProperty == null || map.ShouldMapProperty(p),
        MethodInfo m => map?.ShouldMapMethod == null || map.ShouldMapMethod(m),
        FieldInfo f => map?.ShouldMapField == null || map.ShouldMapField(f),
        _ => true
    };

    private static string Replace(string name, IReadOnlyList<(string Original, string Replacement)> replacements)
    {
        foreach (var (original, replacement) in replacements)
        {
            name = name.Replace(original, replacement);
        }

        return name;
    }

    /// <summary>
    /// Builds a read of <paramref name="access"/> starting at <paramref name="source"/>, which must not be null.
    /// A null intermediate object in a flattened path yields the default value instead of throwing.
    /// </summary>
    public static Expression BuildRead(Expression source, SourceAccess access) => BuildRead(source, access.Steps, 0);

    private static Expression BuildRead(Expression current, IReadOnlyList<MemberInfo> steps, int index)
    {
        var step = Access(current, steps[index]);
        if (index == steps.Count - 1)
        {
            return step;
        }

        if (step.Type.IsValueType)
        {
            return BuildRead(step, steps, index + 1);
        }

        var temp = Expression.Variable(step.Type, "step" + index);
        var rest = BuildRead(temp, steps, index + 1);
        return Expression.Block(
            rest.Type,
            new[] { temp },
            Expression.Assign(temp, step),
            Expression.Condition(
                Expression.Equal(temp, Expression.Constant(null, step.Type)),
                Expression.Default(rest.Type),
                rest));
    }

    private static Expression Access(Expression instance, MemberInfo member) => member switch
    {
        PropertyInfo p => Expression.Property(instance, p),
        FieldInfo f => Expression.Field(instance, f),
        MethodInfo { IsStatic: true } m => Expression.Call(m, Expression.Convert(instance, m.GetParameters()[0].ParameterType)),
        MethodInfo m => Expression.Call(instance, m),
        _ => throw new NotSupportedException($"Member '{member.Name}' of kind {member.MemberType} cannot be read.")
    };

    internal static string StripPrefixesAndSuffixes(string name, IReadOnlyList<string> prefixes, IReadOnlyList<string> suffixes) =>
        Shared.MemberNameMatching.StripPrefixesAndSuffixes(name, prefixes, suffixes);

    internal static bool WordsMatch(IReadOnlyList<string> words1, IReadOnlyList<string> words2) =>
        Shared.MemberNameMatching.WordsMatch(words1, words2);
}
