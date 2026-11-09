using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Configuration;
using AutoMapSharp.Conventions;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp;

/// <summary>
/// Encapsulates mapping definitions and conventions for a logical group of types.
/// Corresponds conceptually to AutoMapper's Profile.
/// </summary>
/// <remarks>
/// Module-level settings such as naming conventions, prefixes, suffixes, value transformers and the null
/// collection policy apply to every map in the module, no matter whether they are set before or after
/// <see cref="DefineMap{TSource, TDestination}"/>. They are read when the <see cref="MapEngine"/> is built.
/// </remarks>
public abstract class MappingModule
{
    private readonly Dictionary<(Type Source, Type Destination), TypeMappingDescriptor> _descriptors = new();
    private readonly List<TypeMappingDescriptor> _ordered = new();
    private readonly List<string> _modulePrefixes = new() { "Get" };
    private readonly List<string> _moduleSuffixes = new();
    private readonly List<string> _destinationPrefixes = new();
    private readonly List<string> _destinationSuffixes = new();
    private readonly List<(string Original, string Replacement)> _replacements = new();
    private readonly List<Type> _extensionTypes = new();

    /// <summary>
    /// Gets all registered type mapping descriptors in this module, in the order they were defined.
    /// </summary>
    public IReadOnlyCollection<TypeMappingDescriptor> TypeMaps => _ordered;

    /// <summary>
    /// Global value transformers applied across mappings configured in this module.
    /// </summary>
    public ValueTransformerCollection ValueTransformers { get; } = new();

    /// <summary>
    /// Gets or sets the source naming convention for this module.
    /// </summary>
    protected INamingConvention SourceNamingConvention { get; set; } = PascalCaseNamingConvention.Instance;

    /// <summary>
    /// Gets or sets the destination naming convention for this module.
    /// </summary>
    protected INamingConvention DestinationNamingConvention { get; set; } = PascalCaseNamingConvention.Instance;

    /// <summary>
    /// Gets or sets whether null source collections produce null or empty destination collections by default.
    /// </summary>
    protected bool AllowNullCollections { get; set; } = false;

    /// <summary>
    /// Decides which source and destination properties take part in mapping. The default maps every public property.
    /// </summary>
    protected Func<PropertyInfo, bool>? ShouldMapProperty { get; set; }

    /// <summary>
    /// Decides which public fields take part in mapping. The default maps every public field that is not read-only on the destination.
    /// </summary>
    protected Func<FieldInfo, bool>? ShouldMapField { get; set; }

    /// <summary>
    /// Decides which parameterless source methods, such as <c>GetTotal()</c>, can act as source members.
    /// </summary>
    protected Func<MethodInfo, bool>? ShouldMapMethod { get; set; }

    internal IReadOnlyList<string> ModulePrefixes => _modulePrefixes;
    internal IReadOnlyList<string> ModuleDestinationPrefixes => _destinationPrefixes;
    internal IReadOnlyList<string> ModuleDestinationSuffixes => _destinationSuffixes;
    internal IReadOnlyList<(string Original, string Replacement)> ModuleReplacements => _replacements;
    internal IReadOnlyList<Type> ModuleExtensionTypes => _extensionTypes;
    internal Func<PropertyInfo, bool>? ModuleShouldMapProperty => ShouldMapProperty;
    internal Func<FieldInfo, bool>? ModuleShouldMapField => ShouldMapField;
    internal Func<MethodInfo, bool>? ModuleShouldMapMethod => ShouldMapMethod;
    internal IReadOnlyList<string> ModuleSuffixes => _moduleSuffixes;
    internal INamingConvention ModuleSourceNamingConvention => SourceNamingConvention;
    internal INamingConvention ModuleDestinationNamingConvention => DestinationNamingConvention;
    internal bool ModuleAllowNullCollections => AllowNullCollections;

    /// <summary>
    /// Recognizes prefixes to strip from source member names during matching.
    /// </summary>
    protected void RecognizePrefixes(params string[] prefixes)
    {
        if (prefixes == null) return;
        foreach (var prefix in prefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && !_modulePrefixes.Contains(prefix))
            {
                _modulePrefixes.Add(prefix);
            }
        }
    }

    /// <summary>
    /// Recognizes suffixes to strip from member names during matching.
    /// </summary>
    protected void RecognizeSuffixes(params string[] suffixes)
    {
        if (suffixes == null) return;
        foreach (var suffix in suffixes)
        {
            if (!string.IsNullOrEmpty(suffix) && !_moduleSuffixes.Contains(suffix))
            {
                _moduleSuffixes.Add(suffix);
            }
        }
    }

    /// <summary>
    /// Removes every recognized source prefix, including the default <c>Get</c>.
    /// </summary>
    protected void ClearPrefixes() => _modulePrefixes.Clear();

    /// <summary>
    /// Recognizes prefixes to strip from destination member names during matching, for example <c>"Dto"</c> in <c>DtoName</c>.
    /// </summary>
    protected void RecognizeDestinationPrefixes(params string[] prefixes) => AddAll(_destinationPrefixes, prefixes);

    /// <summary>
    /// Recognizes suffixes to strip from destination member names during matching.
    /// </summary>
    protected void RecognizeDestinationSuffixes(params string[] suffixes) => AddAll(_destinationSuffixes, suffixes);

    /// <summary>
    /// Replaces text in source member names before matching, for example <c>ReplaceMemberName("Ä", "A")</c>.
    /// </summary>
    protected void ReplaceMemberName(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original)) throw new ArgumentException("The text to replace is required.", nameof(original));
        _replacements.Add((original, replacement ?? string.Empty));
    }

    /// <summary>
    /// Lets extension methods in <paramref name="type"/> act as source members, for example
    /// <c>public static int OrderCount(this Customer c)</c> for a destination member <c>OrderCount</c>.
    /// </summary>
    protected void IncludeSourceExtensionMethods(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (!(type.IsAbstract && type.IsSealed))
        {
            throw new AutoMapSharpConfigurationException($"Type '{type.FullName}' must be a static class that contains extension methods.");
        }

        if (!_extensionTypes.Contains(type)) _extensionTypes.Add(type);
    }

    private static void AddAll(List<string> target, string[]? values)
    {
        if (values == null) return;
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value) && !target.Contains(value)) target.Add(value);
        }
    }

    /// <summary>
    /// Begins configuration of a map between two types given at run time. Either type may be an open generic type
    /// such as <c>typeof(Page&lt;&gt;)</c>, in which case the map is closed for each concrete pair on first use.
    /// </summary>
    protected IMappingDefinition DefineMap(Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));
        if (sourceType.IsGenericTypeDefinition != destinationType.IsGenericTypeDefinition)
        {
            throw new AutoMapSharpConfigurationException("An open generic map needs open generic types on both sides, such as typeof(Page<>) and typeof(PageDto<>).");
        }

        var key = (sourceType, destinationType);
        if (_descriptors.ContainsKey(key))
        {
            throw new AutoMapSharpConfigurationException(
                $"Mapping from '{sourceType.FullName}' to '{destinationType.FullName}' is already defined in module '{GetType().Name}'.");
        }

        var descriptor = new TypeMappingDescriptor(sourceType, destinationType) { Module = this };
        Register(key, descriptor);
        return new NonGenericMappingDefinition(descriptor, this);
    }

    internal IMappingDefinition CreateReverseMap(TypeMappingDescriptor forward)
    {
        var key = (forward.DestinationType, forward.SourceType);
        if (_descriptors.ContainsKey(key))
        {
            throw new AutoMapSharpConfigurationException(
                $"Reverse mapping from '{forward.DestinationType.FullName}' to '{forward.SourceType.FullName}' is already defined in module '{GetType().Name}'.");
        }

        var reverse = new TypeMappingDescriptor(forward.DestinationType, forward.SourceType)
        {
            Module = this,
            IsReverseMap = true,
            AllowNullCollections = forward.AllowNullCollections,
            MemberListToValidate = MemberList.None
        };
        Register(key, reverse);
        return new NonGenericMappingDefinition(reverse, this);
    }

    /// <summary>
    /// Begins configuration of a mapping from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.
    /// </summary>
    /// <exception cref="AutoMapSharpConfigurationException">Thrown when a mapping for this type pair already exists.</exception>
    protected IMappingDefinition<TSource, TDestination> DefineMap<TSource, TDestination>()
    {
        var key = (typeof(TSource), typeof(TDestination));
        if (_descriptors.ContainsKey(key))
        {
            throw new AutoMapSharpConfigurationException(
                $"Mapping from '{typeof(TSource).FullName}' to '{typeof(TDestination).FullName}' is already defined in module '{GetType().Name}'.");
        }

        var descriptor = new TypeMappingDescriptor(typeof(TSource), typeof(TDestination)) { Module = this };
        Register(key, descriptor);
        return new MappingDefinition<TSource, TDestination>(descriptor, this);
    }

    private void Register((Type, Type) key, TypeMappingDescriptor descriptor)
    {
        _descriptors[key] = descriptor;
        _ordered.Add(descriptor);
    }

    internal IMappingDefinition<TDestination, TSource> CreateReverseMap<TSource, TDestination>(TypeMappingDescriptor forwardDescriptor)
    {
        var key = (typeof(TDestination), typeof(TSource));
        if (_descriptors.ContainsKey(key))
        {
            throw new AutoMapSharpConfigurationException(
                $"Reverse mapping from '{typeof(TDestination).FullName}' to '{typeof(TSource).FullName}' is already defined in module '{GetType().Name}'.");
        }

        var reverse = new TypeMappingDescriptor(typeof(TDestination), typeof(TSource))
        {
            Module = this,
            IsReverseMap = true,
            AllowNullCollections = forwardDescriptor.AllowNullCollections,
            // Same as AutoMapper: reverse maps aren't member-validated unless ValidateMembers is called.
            MemberListToValidate = MemberList.None
        };

        // Invert simple redirections: "Dest.X <- src.A.B" becomes "Source.A.B <- dest.X".
        foreach (var rule in forwardDescriptor.Rules.Values)
        {
            if (rule.SourceExpression == null || rule.DestinationMember is not PropertyInfo forwardDestination)
            {
                continue;
            }

            var chain = TryGetPropertyChain(rule.SourceExpression);
            if (chain == null || chain.Count == 0)
            {
                continue;
            }

            var param = Expression.Parameter(typeof(TDestination), "src");
            var reverseSource = Expression.Lambda(Expression.Property(param, forwardDestination), param);

            if (chain.Count == 1)
            {
                reverse.Rules[chain[0].Name] = new PropertyRule(chain[0]) { SourceExpression = reverseSource };
            }
            else
            {
                reverse.Paths.Add(new PathRule(chain, new PropertyRule(chain[chain.Count - 1]) { SourceExpression = reverseSource }));
            }
        }

        Register(key, reverse);
        return new MappingDefinition<TDestination, TSource>(reverse, this);
    }

    /// <summary>
    /// Returns the properties of a lambda body such as <c>s => s.A.B</c>, starting at the lambda parameter,
    /// or null when the body is anything else.
    /// </summary>
    internal static List<PropertyInfo>? TryGetPropertyChain(LambdaExpression lambda)
    {
        var chain = new List<PropertyInfo>();
        Expression? current = lambda.Body;
        if (current is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            current = unary.Operand;
        }

        while (current is MemberExpression member && member.Member is PropertyInfo property)
        {
            chain.Insert(0, property);
            current = member.Expression;
        }

        return current is ParameterExpression ? chain : null;
    }
}
