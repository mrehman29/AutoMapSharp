using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Execution;

namespace AutoMapSharp.Generated;

/// <summary>
/// How a type pair is mapped.
/// </summary>
public enum MapPlanKind
{
    /// <summary>
    /// Typed code generated at compile time by the AutoMapSharp source generator.
    /// </summary>
    Generated,

    /// <summary>
    /// An expression tree compiled at run time.
    /// </summary>
    Compiled,

    /// <summary>
    /// An expression tree run by the expression interpreter, used where dynamic code is not available.
    /// </summary>
    Interpreted
}

/// <summary>
/// Describes how the engine maps one type pair, and why generated code was not used when it was not.
/// </summary>
public sealed class MapPlanInfo
{
    internal MapPlanInfo(MapPlanKind kind, string? reason)
    {
        Kind = kind;
        Reason = reason;
    }

    public MapPlanKind Kind { get; }

    /// <summary>
    /// Why generated code was not used, or null when it was.
    /// </summary>
    public string? Reason { get; }

    public override string ToString() => Reason == null ? Kind.ToString() : $"{Kind} ({Reason})";
}

/// <summary>
/// Holds the maps emitted by the source generator. Generated code registers here from a module initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedMapRegistry
{
    private static readonly ConcurrentDictionary<(Type, Type), GeneratedMapBase> Maps = new();

    /// <summary>
    /// Set the environment variable <c>AUTOMAPSHARP_DISABLE_GENERATED=1</c> to ignore generated maps, for example to
    /// compare them with the compiled engine. CI runs the full test suite both ways.
    /// </summary>
    internal static readonly bool Disabled =
        string.Equals(Environment.GetEnvironmentVariable("AUTOMAPSHARP_DISABLE_GENERATED"), "1", StringComparison.Ordinal);

    /// <summary>
    /// The members the engine reads by reflection to check a generated map. The annotations make the Native AOT
    /// compiler keep exactly this metadata for the mapped types.
    /// </summary>
    internal const DynamicallyAccessedMemberTypes CheckedMembers =
        DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicMethods;

    /// <summary>
    /// Registers a generated map. Called by generated code.
    /// </summary>
    public static void Register<[DynamicallyAccessedMembers(CheckedMembers)] TSource, [DynamicallyAccessedMembers(CheckedMembers)] TDestination>(
        GeneratedMap<TSource, TDestination> map)
    {
        Maps[(typeof(TSource), typeof(TDestination))] = map ?? throw new ArgumentNullException(nameof(map));
    }

    /// <summary>
    /// Keeps the metadata of a type that a generated map reads through, such as an intermediate type of a flattened
    /// member. Called by generated code. Does nothing at run time.
    /// </summary>
    public static void Preserve<[DynamicallyAccessedMembers(CheckedMembers)] T>()
    {
    }

    internal static GeneratedMapBase? Find(Type source, Type destination) =>
        !Disabled && Maps.TryGetValue((source, destination), out var map) ? map : null;

    internal static GeneratedMap<TSource, TDestination>? Find<TSource, TDestination>() =>
        Find(typeof(TSource), typeof(TDestination)) as GeneratedMap<TSource, TDestination>;
}

/// <summary>
/// The part of a generated map that does not depend on its type arguments.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GeneratedMapBase
{
    internal abstract MapperHolder CreateHolder(MapEngine engine, TypeMappingDescriptor? map);
}

/// <summary>
/// State a generated map reads at run time, such as <c>UseValue</c> constants and the null collection policy.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GeneratedMapState
{
    private readonly TypeMappingDescriptor? _map;

    internal GeneratedMapState(TypeMappingDescriptor? map)
    {
        _map = map;
    }

    public bool AllowNullCollections => _map?.AllowNullCollections ?? false;

    public bool PreserveReferences => _map?.PreserveReferences ?? false;

    public T Constant<T>(string member) => (T)_map!.PropertyRules[member].ConstantValue!;
}

/// <summary>
/// One map emitted by the source generator, with the assumptions it was generated from.
/// </summary>
/// <remarks>
/// Before the engine uses the generated code, it checks every assumption against the frozen runtime configuration:
/// the source of each destination member, the pairs assigned without a map, and the element maps of collections.
/// When anything differs, the engine uses its compiled plan instead, so generated code never behaves differently.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GeneratedMap<
    [DynamicallyAccessedMembers(GeneratedMapRegistry.CheckedMembers)] TSource,
    [DynamicallyAccessedMembers(GeneratedMapRegistry.CheckedMembers)] TDestination> : GeneratedMapBase
{
    private readonly Func<TSource, TDestination, bool, ResolutionContext, GeneratedMapState, TDestination> _map;
    private readonly string[] _members;
    private readonly Type[] _directPairs;
    private readonly Type[] _elementPairs;
    private readonly Type[] _convertedPairs;

    /// <param name="map">The generated mapping method.</param>
    /// <param name="members">One entry per destination member: <c>Name=&gt;Source.Path</c>, <c>Name!skip</c> or <c>Name!value</c>.</param>
    /// <param name="directPairs">Source and destination type pairs, flattened, that the code assigns without a map.</param>
    /// <param name="elementPairs">Source and destination element type pairs, flattened, of mapped collections.</param>
    /// <param name="convertedPairs">Type pairs, flattened, that the code converts itself. The engine would use a defined map for them instead.</param>
    public GeneratedMap(
        Func<TSource, TDestination, bool, ResolutionContext, GeneratedMapState, TDestination> map,
        string[] members,
        Type[] directPairs,
        Type[] elementPairs,
        Type[] convertedPairs)
    {
        _convertedPairs = convertedPairs ?? Type.EmptyTypes;
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _members = members ?? Array.Empty<string>();
        _directPairs = directPairs ?? Type.EmptyTypes;
        _elementPairs = elementPairs ?? Type.EmptyTypes;
    }

    internal override MapperHolder CreateHolder(MapEngine engine, TypeMappingDescriptor? map) => new MapperHolder<TSource, TDestination>(engine, map);

    internal Func<TSource, TDestination, bool, ResolutionContext, TDestination> Bind(TypeMappingDescriptor? map)
    {
        var state = new GeneratedMapState(map);
        var generated = _map;
        return (source, destination, useDestination, context) => generated(source, destination, useDestination, context, state);
    }

    /// <summary>
    /// Returns null when the generated code matches the runtime configuration, otherwise the first difference.
    /// </summary>
    internal string? FindMismatch(MapEngine engine, TypeMappingDescriptor? map)
    {
        if (map == null) return "the map is not defined at run time";
        if (map.CustomConverter != null || map.CustomConverterType != null) return "the map uses ConvertUsing";
        if (map.ConstructorExpression != null) return "the map uses ConstructUsing";
        if (map.BeforeMapActions.Count > 0 || map.AfterMapActions.Count > 0) return "the map has BeforeMap or AfterMap callbacks";
        if (map.PathRules.Count > 0) return "the map has ForPath rules";
        if (map.CtorParamRules.Count > 0) return "the map uses ForCtorParam";
        if (map.AsType != null) return "the map uses As<T>()";

        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _members)
        {
            var name = entry.Split(new[] { "=>", "!" }, 2, StringSplitOptions.None)[0];
            expected[name] = entry;
        }

        var actual = SourceMemberMatcher.GetDestinationMembers(typeof(TDestination), map);
        if (actual.Count != expected.Count) return "the destination members differ";

        foreach (var member in actual)
        {
            if (!expected.TryGetValue(member.Name, out var entry)) return $"member '{member.Name}' was not generated";
            var memberType = SourceMemberMatcher.MemberType(member);
            if (map.ValueTransformers.GetTransformers(memberType).Count > 0) return $"member '{member.Name}' has value transformers";

            map.PropertyRules.TryGetValue(member.Name, out var rule);
            if (entry.EndsWith("!skip", StringComparison.Ordinal))
            {
                if (rule?.IsIgnored != true) return $"member '{member.Name}' is not skipped at run time";
                continue;
            }

            if (entry.EndsWith("!value", StringComparison.Ordinal))
            {
                if (rule == null || !rule.HasConstantValue || HasOtherOptions(rule, constantAllowed: true)) return $"member '{member.Name}' is configured differently at run time";
                continue;
            }

            if (rule != null && (rule.IsIgnored || rule.HasConstantValue || HasOtherOptions(rule, constantAllowed: false)))
            {
                return $"member '{member.Name}' is configured differently at run time";
            }

            var access = SourceMemberMatcher.Find(typeof(TSource), member.Name, map);
            var path = access == null ? "" : string.Join(".", access.Steps.Select(s => s.Name));
            if (!string.Equals(entry, member.Name + "=>" + path, StringComparison.Ordinal))
            {
                return $"member '{member.Name}' reads a different source member at run time";
            }
        }

        for (int i = 0; i + 1 < _directPairs.Length; i += 2)
        {
            if (engine.HasExplicitMap(_directPairs[i], _directPairs[i + 1]) && !BuiltInConversions.IsSimple(_directPairs[i + 1]))
            {
                return $"a map is defined from {_directPairs[i].Name} to {_directPairs[i + 1].Name}, which generated code assigns directly";
            }
        }

        for (int i = 0; i + 1 < _convertedPairs.Length; i += 2)
        {
            if (engine.HasExplicitMap(_convertedPairs[i], _convertedPairs[i + 1]))
            {
                return $"a map is defined from {_convertedPairs[i].Name} to {_convertedPairs[i + 1].Name}, which generated code converts directly";
            }
        }

        for (int i = 0; i + 1 < _elementPairs.Length; i += 2)
        {
            if (engine.Configuration.Find(_elementPairs[i], _elementPairs[i + 1])?.EqualityComparison != null)
            {
                return $"the element map from {_elementPairs[i].Name} to {_elementPairs[i + 1].Name} uses EqualityComparison";
            }
        }

        return null;
    }

    private static bool HasOtherOptions(PropertyRule rule, bool constantAllowed) =>
        rule.SourceExpression != null || rule.SourceMemberName != null || rule.CustomResolverType != null || rule.ValueConverterType != null ||
        rule.Condition != null || rule.ValueCondition != null || rule.HasFallbackValue || rule.UseDestinationValue != null ||
        rule.Transformers.Count > 0 || (!constantAllowed && rule.HasConstantValue);
}

/// <summary>
/// Helpers called by generated maps, so generated code behaves exactly like compiled plans.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedMapHelpers
{
    public static void Created<TSource, TDestination>(ResolutionContext context, GeneratedMapState state, TSource source, TDestination destination)
    {
        if (state.PreserveReferences && source is not null && destination is not null)
        {
            context.RegisterMappedInstance(source, typeof(TDestination), destination);
        }
    }

    public static void BeforeMap(ResolutionContext context, object? source, object? destination)
    {
        if (context.HasOperationCallbacks && source != null && destination != null) context.OnBeforeMap(source, destination);
    }

    public static void AfterMap(ResolutionContext context, object? source, object? destination)
    {
        if (context.HasOperationCallbacks && source != null && destination != null) context.OnAfterMap(source, destination);
    }

    public static Exception MemberError(Exception inner, Type sourceType, Type destinationType, string stage)
    {
        var what = stage switch
        {
            "constructor" => "creating the destination object",
            "BeforeMap" => "running a BeforeMap callback",
            "AfterMap" => "running an AfterMap callback",
            _ => $"mapping member '{stage}'"
        };

        return new AutoMapSharpMappingException($"Error {what} from '{sourceType.FullName}' to '{destinationType.FullName}': {inner.Message}", sourceType, destinationType, inner);
    }

    /// <summary>
    /// Maps a nested object through the engine, which applies polymorphism, reference preservation and depth limits.
    /// </summary>
    public static TDestination MapNested<TSource, TDestination>(ResolutionContext context, TSource value, TDestination existing, bool useExisting)
    {
        if (value is null) return default!;
        var engine = ((MapEngine)context.Engine).Root;
        return engine.GetHolder<TSource, TDestination>().Map(value, existing, useExisting, context);
    }

    /// <summary>
    /// Maps a collection to a list, or refills an existing mutable collection in place, like compiled plans do.
    /// </summary>
    public static TCollection MapToList<TCollection, TSourceItem, TDestinationItem>(
        ResolutionContext context, GeneratedMapState state, IEnumerable<TSourceItem>? items, TCollection existing, Func<TSourceItem, ResolutionContext, TDestinationItem> convert)
        where TCollection : class, IEnumerable<TDestinationItem>
    {
        var source = items ?? (state.AllowNullCollections ? null : Enumerable.Empty<TSourceItem>());
        if (source == null) return null!;

        if (CollectionRuntime.TryFillExisting(existing, source, convert, context))
        {
            return existing;
        }

        return (TCollection)(object)CollectionRuntime.ToList(source, convert, context);
    }

    public static TDestinationItem[] MapToArray<TSourceItem, TDestinationItem>(
        ResolutionContext context, GeneratedMapState state, IEnumerable<TSourceItem>? items, Func<TSourceItem, ResolutionContext, TDestinationItem> convert)
    {
        var source = items ?? (state.AllowNullCollections ? null : Enumerable.Empty<TSourceItem>());
        return source == null ? null! : CollectionRuntime.ToArray(source, convert, context);
    }

    public static TEnum ParseEnum<TEnum>(string value) where TEnum : struct => EnumConversions.Parse<TEnum>(value);

    public static TTo MapEnumByName<TFrom, TTo>(TFrom value) where TFrom : struct where TTo : struct => EnumConversions.MapByName<TFrom, TTo>(value);

    public static IFormatProvider Invariant => CultureInfo.InvariantCulture;
}
