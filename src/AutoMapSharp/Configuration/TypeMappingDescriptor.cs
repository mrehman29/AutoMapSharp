using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Conventions;

namespace AutoMapSharp.Configuration;

/// <summary>
/// Configuration rules for one destination member.
/// </summary>
/// <remarks>
/// Rules are set through the fluent API on <see cref="MappingModule"/> and are read-only afterwards.
/// </remarks>
public sealed class PropertyRule
{
    /// <summary>The destination property or field this rule configures.</summary>
    public MemberInfo DestinationMember { get; }
    /// <summary>The source expression that supplies the value, set with <c>MapSource</c>. Null when the value comes from conventions or another rule.</summary>
    public LambdaExpression? SourceExpression { get; internal set; }
    /// <summary>True when the member is skipped with <c>Skip()</c> and is never written.</summary>
    public bool IsIgnored { get; internal set; }
    /// <summary>The constant value written to the member, set with <c>UseValue</c>. Only meaningful when <c>HasConstantValue</c> is true.</summary>
    public object? ConstantValue { get; internal set; }
    /// <summary>True when a constant value has been configured, which distinguishes a configured null from no constant.</summary>
    public bool HasConstantValue { get; internal set; }
    /// <summary>A condition evaluated before the value is resolved. The member is only mapped when it returns true.</summary>
    public Delegate? Condition { get; internal set; }
    /// <summary>The value used when the resolved source value is null, set with <c>Fallback</c>.</summary>
    public object? FallbackValue { get; internal set; }
    /// <summary>True when <see cref="FallbackValue"/> has been set, which distinguishes a configured null from no fallback.</summary>
    public bool HasFallbackValue { get; internal set; }
    /// <summary>An <see cref="IPropertyResolver{TSource, TDestination, TDestinationMember}"/> type that resolves the value for this member.</summary>
    public Type? CustomResolverType { get; internal set; }
    /// <summary>True when the member is only included in a projection if it is requested explicitly in <c>ProjectOnto</c>.</summary>
    public bool ExplicitExpansion { get; internal set; }

    /// <summary>
    /// A condition that runs after the value is resolved and receives it: <c>Func&lt;TSource, TDestination, TMember, bool&gt;</c>
    /// or <c>Func&lt;TSource, TDestination, TMember, TMember, bool&gt;</c>.
    /// </summary>
    public Delegate? ValueCondition { get; internal set; }

    /// <summary>
    /// True for UseDestinationValue, false for DoNotUseDestinationValue, null for the default behavior.
    /// </summary>
    public bool? UseDestinationValue { get; internal set; }

    /// <summary>
    /// The name of the source member to read, set by the non-generic <c>MapSource(string)</c>.
    /// </summary>
    public string? SourceMemberName { get; internal set; }

    /// <summary>
    /// An <see cref="IValueConverter{TSourceMember, TDestinationMember}"/> type applied to the source value.
    /// </summary>
    public Type? ValueConverterType { get; internal set; }

    internal readonly List<Delegate> MemberTransformers = new();

    /// <summary>
    /// Transformers for this member only, applied after module-level transformers.
    /// </summary>
    public IReadOnlyList<Delegate> Transformers => MemberTransformers;

    /// <summary>Creates a rule for <paramref name="destinationMember"/>.</summary>
    /// <param name="destinationMember">The destination property or field the rule configures.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destinationMember"/> is null.</exception>
    public PropertyRule(MemberInfo destinationMember)
    {
        DestinationMember = destinationMember ?? throw new ArgumentNullException(nameof(destinationMember));
    }

    internal PropertyRule Clone() => (PropertyRule)MemberwiseClone();
}

/// <summary>
/// A rule for one constructor parameter, configured with <c>ForCtorParam</c>.
/// </summary>
public sealed class CtorParamRule
{
    /// <summary>The name of the destination constructor parameter this rule configures.</summary>
    public string ParameterName { get; }
    /// <summary>The source expression that supplies the value, set with <c>MapSource</c>. Null when the value comes from conventions or another rule.</summary>
    public LambdaExpression? SourceExpression { get; internal set; }
    /// <summary>The constant value written to the member, set with <c>UseValue</c>. Only meaningful when <c>HasConstantValue</c> is true.</summary>
    public object? ConstantValue { get; internal set; }
    /// <summary>True when a constant value has been configured, which distinguishes a configured null from no constant.</summary>
    public bool HasConstantValue { get; internal set; }

    internal CtorParamRule(string parameterName)
    {
        ParameterName = parameterName;
    }
}

/// <summary>
/// A rule for a nested destination member, configured with <c>ForPath(d => d.Customer.Name, ...)</c>.
/// </summary>
public sealed class PathRule
{
    /// <summary>
    /// The chain of destination properties from the root destination object to the target member.
    /// </summary>
    public IReadOnlyList<PropertyInfo> Path { get; }

    /// <summary>
    /// The rule applied to the last member of the path.
    /// </summary>
    public PropertyRule Rule { get; }

    /// <summary>
    /// True when the rule was created automatically by <c>ReverseMap</c> unflattening.
    /// </summary>
    public bool IsImplicit { get; }

    internal PathRule(IReadOnlyList<PropertyInfo> path, PropertyRule rule, bool isImplicit = false)
    {
        Path = path;
        Rule = rule;
        IsImplicit = isImplicit;
    }

    internal string Key => string.Join(".", System.Linq.Enumerable.Select(Path, p => p.Name));
}

/// <summary>
/// The configuration for mapping one source type to one destination type.
/// </summary>
/// <remarks>
/// <see cref="IMapEngine.FindDescriptor"/> returns the effective, frozen configuration: module settings are applied
/// and <c>IncludeBase</c> rules are merged in. It cannot be changed after the engine is built.
/// </remarks>
public sealed class TypeMappingDescriptor
{
    internal readonly Dictionary<string, PropertyRule> Rules = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<PathRule> Paths = new();
    internal readonly List<Delegate> BeforeActions = new();
    internal readonly List<Delegate> AfterActions = new();
    internal readonly List<(Type DerivedSource, Type DerivedDestination)> DerivedTypes = new();
    internal readonly List<string> Prefixes = new();
    internal readonly List<string> Suffixes = new();
    internal readonly List<string> DestinationPrefixList = new();
    internal readonly List<string> DestinationSuffixList = new();
    internal readonly List<(string Original, string Replacement)> Replacements = new();
    internal readonly Dictionary<string, CtorParamRule> CtorRules = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<(Type BaseSource, Type BaseDestination)> BaseTypes = new();
    internal readonly HashSet<string> UnvalidatedSourceMemberNames = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<Type> ExtensionTypes = new();

    /// <summary>The source type of the map.</summary>
    public Type SourceType { get; }
    /// <summary>The destination type of the map.</summary>
    public Type DestinationType { get; }

    /// <summary>The rules for individual destination members, keyed by member name (case-insensitive).</summary>
    public IReadOnlyDictionary<string, PropertyRule> PropertyRules => Rules;
    /// <summary>The rules for nested destination members, configured with <c>ForPath</c> or created by <c>ReverseMap</c> unflattening.</summary>
    public IReadOnlyList<PathRule> PathRules => Paths;
    /// <summary>A delegate that replaces member-by-member mapping for the whole type, set with <c>ConvertUsing</c>.</summary>
    public Delegate? CustomConverter { get; internal set; }
    /// <summary>An <see cref="ITypeConverter{TSource, TDestination}"/> type that replaces member-by-member mapping for the whole type.</summary>
    public Type? CustomConverterType { get; internal set; }
    /// <summary>A delegate that creates the destination object, set with <c>ConstructUsing</c>.</summary>
    public Delegate? ConstructorExpression { get; internal set; }
    /// <summary>Actions run before members are mapped, added with <c>BeforeMap</c>.</summary>
    public IReadOnlyList<Delegate> BeforeMapActions => BeforeActions;
    /// <summary>Actions run after members are mapped, added with <c>AfterMap</c>.</summary>
    public IReadOnlyList<Delegate> AfterMapActions => AfterActions;
    /// <summary>The maximum recursion depth for this map, set with <c>MaxDepth</c>. Null means no limit.</summary>
    public int? MaxDepth { get; internal set; }
    /// <summary>True when already-mapped source objects are reused instead of mapped again, which keeps cyclic graphs intact.</summary>
    public bool PreserveReferences { get; internal set; }
    /// <summary>Whether null source collections map to null rather than empty collections. Null means the module or engine default applies.</summary>
    public bool? AllowNullCollections { get; internal set; }
    /// <summary>Derived type pairs registered with <c>Include</c>, used to pick a more specific map at runtime.</summary>
    public IReadOnlyList<(Type DerivedSource, Type DerivedDestination)> IncludedDerivedTypes => DerivedTypes;
    /// <summary>The first base type pair registered with <c>IncludeBase</c>, or null if there is none.</summary>
    public (Type BaseSource, Type BaseDestination)? IncludedBaseType => BaseTypes.Count > 0 ? BaseTypes[0] : null;
    /// <summary>All base type pairs registered with <c>IncludeBase</c>, whose rules are inherited by this map.</summary>
    public IReadOnlyList<(Type BaseSource, Type BaseDestination)> IncludedBaseTypes => BaseTypes;
    /// <summary>The rules for destination constructor parameters, keyed by parameter name (case-insensitive).</summary>
    public IReadOnlyDictionary<string, CtorParamRule> CtorParamRules => CtorRules;
    /// <summary>True when every map whose types derive from this map's types is included automatically, set with <c>IncludeAllDerived</c>.</summary>
    public bool IncludeAllDerivedTypes { get; internal set; }
    /// <summary>The type whose map is used in place of this one, set with <c>As&lt;T&gt;()</c>.</summary>
    public Type? AsType { get; internal set; }
    /// <summary>Which side's members configuration validation checks. Defaults to <see cref="MemberList.Destination"/>.</summary>
    public MemberList MemberListToValidate { get; internal set; } = MemberList.Destination;
    /// <summary>Source members excluded from validation when <see cref="MemberListToValidate"/> is <see cref="MemberList.Source"/>.</summary>
    public IReadOnlyCollection<string> UnvalidatedSourceMembers => UnvalidatedSourceMemberNames;

    /// <summary>Prefixes stripped from source member names when matching by convention.</summary>
    public IReadOnlyList<string> RecognizedPrefixes => Prefixes;
    /// <summary>Suffixes stripped from source member names when matching by convention.</summary>
    public IReadOnlyList<string> RecognizedSuffixes => Suffixes;
    /// <summary>Prefixes stripped from destination member names when matching by convention.</summary>
    public IReadOnlyList<string> RecognizedDestinationPrefixes => DestinationPrefixList;
    /// <summary>Suffixes stripped from destination member names when matching by convention.</summary>
    public IReadOnlyList<string> RecognizedDestinationSuffixes => DestinationSuffixList;
    /// <summary>Text replacements applied to member names before they are matched.</summary>
    public IReadOnlyList<(string Original, string Replacement)> MemberNameReplacements => Replacements;
    /// <summary>Static types whose extension methods on the source type are treated as source members.</summary>
    public IReadOnlyList<Type> SourceExtensionMethodTypes => ExtensionTypes;
    /// <summary>A filter deciding which properties take part in convention mapping. Null means all public properties.</summary>
    public Func<PropertyInfo, bool>? ShouldMapProperty { get; internal set; }
    /// <summary>A filter deciding which fields take part in convention mapping. Null means the default field rules apply.</summary>
    public Func<FieldInfo, bool>? ShouldMapField { get; internal set; }
    /// <summary>A filter deciding which parameterless methods can act as source members. Null means the default method rules apply.</summary>
    public Func<MethodInfo, bool>? ShouldMapMethod { get; internal set; }
    /// <summary>How source member names are split into words for matching. Defaults to PascalCase.</summary>
    public INamingConvention SourceNamingConvention { get; internal set; } = PascalCaseNamingConvention.Instance;
    /// <summary>How destination member names are split into words for matching. Defaults to PascalCase.</summary>
    public INamingConvention DestinationNamingConvention { get; internal set; } = PascalCaseNamingConvention.Instance;

    /// <summary>Value transformers applied to every member of this map.</summary>
    public ValueTransformerCollection ValueTransformers { get; } = new();

    /// <summary>
    /// How an item of a source collection is matched to an item of an existing destination collection, set with
    /// <c>EqualityComparison</c> from the AutoMapSharp.Collections package. A <c>Func&lt;TSource, TDestination, bool&gt;</c>.
    /// </summary>
    public Delegate? EqualityComparison { get; internal set; }

    /// <summary>
    /// True when this map was created by <c>ReverseMap()</c>.
    /// </summary>
    public bool IsReverseMap { get; internal set; }

    /// <summary>
    /// The module that defined this map.
    /// </summary>
    internal MappingModule? Module { get; set; }

    /// <summary>Creates an empty configuration for mapping <paramref name="sourceType"/> to <paramref name="destinationType"/>.</summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <exception cref="ArgumentNullException">Either type is null.</exception>
    public TypeMappingDescriptor(Type sourceType, Type destinationType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
    }
}
