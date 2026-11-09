using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Flattening;

namespace AutoMapSharp.Execution;

/// <summary>
/// The frozen configuration of an engine. Built once from the modules and never changed afterwards.
/// </summary>
internal sealed class FrozenConfiguration
{
    private readonly Dictionary<(Type Source, Type Destination), TypeMappingDescriptor> _defined = new();
    private readonly List<TypeMappingDescriptor> _definedInOrder = new();
    private readonly List<TypeMappingDescriptor> _templates = new();
    private readonly Dictionary<(Type Source, Type Destination), List<(Type, Type)>> _implicitBases = new();
    private readonly Dictionary<(Type Source, Type Destination), TypeMappingDescriptor> _effective = new();
    private readonly ConcurrentDictionary<(Type Source, Type Destination), TypeMappingDescriptor?> _closedGenerics = new();
    private readonly List<TypeMappingDescriptor> _ordered = new();
    private readonly object _closeLock = new();

    public FrozenConfiguration(IEnumerable<MappingModule>? modules)
    {
        var definedBy = new Dictionary<(Type, Type), MappingModule>();

        foreach (var module in modules ?? Enumerable.Empty<MappingModule>())
        {
            if (module == null) continue;

            foreach (var descriptor in module.TypeMaps)
            {
                var key = (descriptor.SourceType, descriptor.DestinationType);
                if (definedBy.TryGetValue(key, out var first))
                {
                    throw new AutoMapSharpConfigurationException(
                        $"Mapping from '{descriptor.SourceType.FullName}' to '{descriptor.DestinationType.FullName}' is defined in module " +
                        $"'{first.GetType().Name}' and again in module '{module.GetType().Name}'. Each type pair can only be defined once.");
                }

                definedBy[key] = module;

                if (descriptor.SourceType.IsGenericTypeDefinition || descriptor.DestinationType.IsGenericTypeDefinition)
                {
                    _templates.Add(descriptor);
                    continue;
                }

                _defined[key] = descriptor;
                _definedInOrder.Add(descriptor);
            }
        }

        foreach (var baseMap in _definedInOrder.Where(d => d.IncludeAllDerivedTypes))
        {
            foreach (var derived in _definedInOrder)
            {
                if (ReferenceEquals(derived, baseMap) ||
                    !baseMap.SourceType.IsAssignableFrom(derived.SourceType) ||
                    !baseMap.DestinationType.IsAssignableFrom(derived.DestinationType))
                {
                    continue;
                }

                var baseKey = (baseMap.SourceType, baseMap.DestinationType);
                var derivedKey = (derived.SourceType, derived.DestinationType);
                if (derived.BaseTypes.Contains(baseKey)) continue;

                if (!_implicitBases.TryGetValue(derivedKey, out var list))
                {
                    _implicitBases[derivedKey] = list = new List<(Type, Type)>();
                }

                list.Add(baseKey);
            }
        }

        foreach (var descriptor in _definedInOrder)
        {
            _ordered.Add(GetEffective((descriptor.SourceType, descriptor.DestinationType), new HashSet<(Type, Type)>()));
        }
    }

    /// <summary>
    /// Effective closed maps in the order they were defined.
    /// </summary>
    public IReadOnlyList<TypeMappingDescriptor> Maps => _ordered;

    public TypeMappingDescriptor? Find(Type source, Type destination)
    {
        if (_effective.TryGetValue((source, destination), out var map))
        {
            return map;
        }

        if (_templates.Count == 0 || !source.IsGenericType && !destination.IsGenericType)
        {
            return null;
        }

        return _closedGenerics.GetOrAdd((source, destination), key => CloseTemplate(key.Source, key.Destination));
    }

    public bool Contains(Type source, Type destination) => Find(source, destination) != null;

    private TypeMappingDescriptor? CloseTemplate(Type source, Type destination)
    {
        var template = _templates.FirstOrDefault(t => Matches(t.SourceType, source) && Matches(t.DestinationType, destination));
        if (template == null)
        {
            return null;
        }

        var closed = new TypeMappingDescriptor(source, destination)
        {
            Module = template.Module,
            IsReverseMap = template.IsReverseMap,
            MaxDepth = template.MaxDepth,
            PreserveReferences = template.PreserveReferences,
            AllowNullCollections = template.AllowNullCollections,
            MemberListToValidate = template.MemberListToValidate,
            CustomConverterType = CloseConverter(template.CustomConverterType, source, destination)
        };

        foreach (var rule in template.Rules)
        {
            closed.Rules[rule.Key] = rule.Value;
        }

        lock (_closeLock)
        {
            return BuildEffective(closed, new HashSet<(Type, Type)>());
        }
    }

    private static bool Matches(Type pattern, Type candidate) =>
        pattern.IsGenericTypeDefinition
            ? candidate.IsGenericType && candidate.GetGenericTypeDefinition() == pattern
            : pattern == candidate;

    private static Type? CloseConverter(Type? converter, Type source, Type destination)
    {
        if (converter == null || !converter.IsGenericTypeDefinition)
        {
            return converter;
        }

        var arity = converter.GetGenericArguments().Length;
        var destinationArgs = destination.IsGenericType ? destination.GetGenericArguments() : Type.EmptyTypes;
        var sourceArgs = source.IsGenericType ? source.GetGenericArguments() : Type.EmptyTypes;
        var args = destinationArgs.Length == arity ? destinationArgs : sourceArgs.Length == arity ? sourceArgs : sourceArgs.Concat(destinationArgs).ToArray();
        if (args.Length != arity)
        {
            throw new AutoMapSharpConfigurationException(
                $"Open generic converter '{converter.FullName}' cannot be closed for the map from '{source.FullName}' to '{destination.FullName}'.");
        }

        return converter.MakeGenericType(args);
    }

    private TypeMappingDescriptor GetEffective((Type Source, Type Destination) key, HashSet<(Type, Type)> visiting)
    {
        if (_effective.TryGetValue(key, out var done))
        {
            return done;
        }

        if (!_defined.TryGetValue(key, out var own))
        {
            return Find(key.Source, key.Destination)
                   ?? throw new AutoMapSharpConfigurationException($"No map is defined from '{key.Source.FullName}' to '{key.Destination.FullName}'.");
        }

        if (!visiting.Add(key))
        {
            throw new AutoMapSharpConfigurationException(
                $"IncludeBase on the map from '{key.Source.FullName}' to '{key.Destination.FullName}' forms a cycle.");
        }

        var effective = BuildEffective(own, visiting);
        _effective[key] = effective;
        visiting.Remove(key);
        return effective;
    }

    private TypeMappingDescriptor BuildEffective(TypeMappingDescriptor own, HashSet<(Type, Type)> visiting)
    {
        var module = own.Module;
        var effective = new TypeMappingDescriptor(own.SourceType, own.DestinationType)
        {
            Module = module,
            IsReverseMap = own.IsReverseMap,
            CustomConverter = own.CustomConverter,
            CustomConverterType = own.CustomConverterType,
            ConstructorExpression = own.ConstructorExpression,
            MaxDepth = own.MaxDepth,
            PreserveReferences = own.PreserveReferences,
            AllowNullCollections = own.AllowNullCollections ?? module?.ModuleAllowNullCollections ?? false,
            IncludeAllDerivedTypes = own.IncludeAllDerivedTypes,
            AsType = own.AsType,
            MemberListToValidate = own.MemberListToValidate,
            EqualityComparison = own.EqualityComparison
        };

        // Base maps first, so the map's own configuration wins.
        var key = (own.SourceType, own.DestinationType);
        var bases = own.BaseTypes.ToList();
        if (_implicitBases.TryGetValue(key, out var implicitBases))
        {
            bases.AddRange(implicitBases.Where(b => !bases.Contains(b)));
        }

        foreach (var baseKey in bases)
        {
            effective.BaseTypes.Add(baseKey);
            if (!_defined.ContainsKey(baseKey) && Find(baseKey.Item1, baseKey.Item2) == null)
            {
                throw new AutoMapSharpConfigurationException(
                    $"The map from '{own.SourceType.FullName}' to '{own.DestinationType.FullName}' calls IncludeBase<{baseKey.Item1.Name}, {baseKey.Item2.Name}>(), " +
                    "but no map is defined for that base pair.");
            }

            var baseMap = GetEffective(baseKey, visiting);
            foreach (var rule in baseMap.Rules) effective.Rules[rule.Key] = rule.Value;
            foreach (var rule in baseMap.CtorRules) effective.CtorRules[rule.Key] = rule.Value;
            effective.Paths.AddRange(baseMap.Paths.Where(p => !p.IsImplicit));
            effective.BeforeActions.AddRange(baseMap.BeforeActions);
            effective.AfterActions.AddRange(baseMap.AfterActions);
            effective.MaxDepth ??= baseMap.MaxDepth;
            effective.PreserveReferences |= baseMap.PreserveReferences;
        }

        foreach (var rule in own.Rules) effective.Rules[rule.Key] = rule.Value;
        foreach (var rule in own.CtorRules) effective.CtorRules[rule.Key] = rule.Value;

        foreach (var path in own.Paths)
        {
            effective.Paths.RemoveAll(p => string.Equals(p.Key, path.Key, StringComparison.OrdinalIgnoreCase));
            effective.Paths.Add(path);
        }

        effective.BeforeActions.AddRange(own.BeforeActions);
        effective.AfterActions.AddRange(own.AfterActions);
        effective.DerivedTypes.AddRange(own.DerivedTypes);
        foreach (var name in own.UnvalidatedSourceMemberNames) effective.UnvalidatedSourceMemberNames.Add(name);

        // Read module conventions now so their position in the constructor doesn't matter.
        AddDistinct(effective.Prefixes, module?.ModulePrefixes ?? new[] { "Get" });
        AddDistinct(effective.Prefixes, own.Prefixes);
        AddDistinct(effective.Suffixes, module?.ModuleSuffixes ?? Array.Empty<string>());
        AddDistinct(effective.Suffixes, own.Suffixes);

        if (module != null)
        {
            AddDistinct(effective.DestinationPrefixList, module.ModuleDestinationPrefixes);
            AddDistinct(effective.DestinationSuffixList, module.ModuleDestinationSuffixes);
            effective.Replacements.AddRange(module.ModuleReplacements);
            effective.ExtensionTypes.AddRange(module.ModuleExtensionTypes);
            effective.SourceNamingConvention = own.IsReverseMap ? module.ModuleDestinationNamingConvention : module.ModuleSourceNamingConvention;
            effective.DestinationNamingConvention = own.IsReverseMap ? module.ModuleSourceNamingConvention : module.ModuleDestinationNamingConvention;
            effective.ShouldMapProperty = module.ModuleShouldMapProperty;
            effective.ShouldMapField = module.ModuleShouldMapField;
            effective.ShouldMapMethod = module.ModuleShouldMapMethod;
            effective.ValueTransformers.CopyFrom(module.ValueTransformers);
        }

        effective.ValueTransformers.CopyFrom(own.ValueTransformers);

        foreach (var member in SourceMemberMatcher.GetDestinationMembers(effective.DestinationType, effective))
        {
            if (effective.Rules.ContainsKey(member.Name)) continue;

            if (member.IsDefined(typeof(SkipMapAttribute), true))
            {
                effective.Rules[member.Name] = new PropertyRule(member) { IsIgnored = true };
            }
            else if (member.GetCustomAttribute<SourceMemberAttribute>(true) is { } sourceMember)
            {
                effective.Rules[member.Name] = new PropertyRule(member) { SourceMemberName = sourceMember.Name };
            }
        }

        if (effective.IsReverseMap)
        {
            AddImplicitUnflattening(effective);
        }

        return effective;
    }

    private static void AddDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!target.Contains(value)) target.Add(value);
        }
    }

    private static void AddImplicitUnflattening(TypeMappingDescriptor map)
    {
        var destinationMembers = SourceMemberMatcher.GetDestinationMembers(map.DestinationType, map)
            .Select(p => p.Name)
            .ToList();

        foreach (var sourceProperty in SourceMemberMatcher.GetReadableProperties(map.SourceType))
        {
            if (destinationMembers.Any(n => string.Equals(n, sourceProperty.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var steps = FlatteningResolver.ResolvePath(map.DestinationType, sourceProperty.Name);
            if (steps == null || steps.Count < 2 || steps.Any(s => s.Member is not PropertyInfo))
            {
                continue;
            }

            var path = steps.Select(s => (PropertyInfo)s.Member).ToList();
            var key = string.Join(".", path.Select(p => p.Name));
            if (map.Rules.ContainsKey(path[0].Name) || map.Paths.Any(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!CanCreatePath(path))
            {
                continue;
            }

            var param = Expression.Parameter(map.SourceType, "src");
            var source = Expression.Lambda(Expression.Property(param, sourceProperty), param);
            map.Paths.Add(new PathRule(path, new PropertyRule(path[path.Count - 1]) { SourceExpression = source }, isImplicit: true));
        }
    }

    internal static bool CanCreatePath(IReadOnlyList<PropertyInfo> path)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            var step = path[i];
            var type = step.PropertyType;
            if (step.GetGetMethod(true) == null || step.GetSetMethod(true) == null || type.IsValueType || type.IsAbstract ||
                type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null) == null)
            {
                return false;
            }
        }

        return path[path.Count - 1].GetSetMethod(true) != null;
    }
}
