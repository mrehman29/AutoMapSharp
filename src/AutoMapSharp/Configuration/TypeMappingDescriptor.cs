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
    public MemberInfo DestinationMember { get; }
    public LambdaExpression? SourceExpression { get; internal set; }
    public bool IsIgnored { get; internal set; }
    public object? ConstantValue { get; internal set; }
    public bool HasConstantValue { get; internal set; }
    public Delegate? Condition { get; internal set; }
    public object? FallbackValue { get; internal set; }
    public bool HasFallbackValue { get; internal set; }
    public Type? CustomResolverType { get; internal set; }
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
    public string ParameterName { get; }
    public LambdaExpression? SourceExpression { get; internal set; }
    public object? ConstantValue { get; internal set; }
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

    public Type SourceType { get; }
    public Type DestinationType { get; }

    public IReadOnlyDictionary<string, PropertyRule> PropertyRules => Rules;
    public IReadOnlyList<PathRule> PathRules => Paths;
    public Delegate? CustomConverter { get; internal set; }
    public Type? CustomConverterType { get; internal set; }
    public Delegate? ConstructorExpression { get; internal set; }
    public IReadOnlyList<Delegate> BeforeMapActions => BeforeActions;
    public IReadOnlyList<Delegate> AfterMapActions => AfterActions;
    public int? MaxDepth { get; internal set; }
    public bool PreserveReferences { get; internal set; }
    public bool? AllowNullCollections { get; internal set; }
    public IReadOnlyList<(Type DerivedSource, Type DerivedDestination)> IncludedDerivedTypes => DerivedTypes;
    public (Type BaseSource, Type BaseDestination)? IncludedBaseType => BaseTypes.Count > 0 ? BaseTypes[0] : null;
    public IReadOnlyList<(Type BaseSource, Type BaseDestination)> IncludedBaseTypes => BaseTypes;
    public IReadOnlyDictionary<string, CtorParamRule> CtorParamRules => CtorRules;
    public bool IncludeAllDerivedTypes { get; internal set; }
    public Type? AsType { get; internal set; }
    public MemberList MemberListToValidate { get; internal set; } = MemberList.Destination;
    public IReadOnlyCollection<string> UnvalidatedSourceMembers => UnvalidatedSourceMemberNames;

    public IReadOnlyList<string> RecognizedPrefixes => Prefixes;
    public IReadOnlyList<string> RecognizedSuffixes => Suffixes;
    public IReadOnlyList<string> RecognizedDestinationPrefixes => DestinationPrefixList;
    public IReadOnlyList<string> RecognizedDestinationSuffixes => DestinationSuffixList;
    public IReadOnlyList<(string Original, string Replacement)> MemberNameReplacements => Replacements;
    public IReadOnlyList<Type> SourceExtensionMethodTypes => ExtensionTypes;
    public Func<PropertyInfo, bool>? ShouldMapProperty { get; internal set; }
    public Func<FieldInfo, bool>? ShouldMapField { get; internal set; }
    public Func<MethodInfo, bool>? ShouldMapMethod { get; internal set; }
    public INamingConvention SourceNamingConvention { get; internal set; } = PascalCaseNamingConvention.Instance;
    public INamingConvention DestinationNamingConvention { get; internal set; } = PascalCaseNamingConvention.Instance;

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

    public TypeMappingDescriptor(Type sourceType, Type destinationType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
    }
}
