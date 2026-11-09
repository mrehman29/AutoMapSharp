using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Execution;
using AutoMapSharp.Validation;

namespace AutoMapSharp;

/// <summary>
/// Default implementation of <see cref="IMapEngine"/>.
/// </summary>
/// <remarks>
/// The engine freezes its configuration when it is constructed. Each source and destination type pair is compiled
/// into one expression-tree plan the first time it is mapped, and the compiled plan is reused for every later call.
/// The engine is thread-safe and is meant to be created once and shared.
/// </remarks>
public sealed class MapEngine : IMapEngine
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly MapEngine _root;
    private readonly ConcurrentDictionary<(Type Source, Type Destination), MapperHolder> _holders;
    private readonly ConcurrentDictionary<(Type Source, Type Destination), bool> _explicitMapCache;

    /// <summary>
    /// Initializes a new instance of <see cref="MapEngine"/> with one or more modules.
    /// </summary>
    public MapEngine(params MappingModule[] modules) : this((IEnumerable<MappingModule>)modules, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MapEngine"/> with optional modules and service provider.
    /// </summary>
    /// <exception cref="AutoMapSharpConfigurationException">Two modules define the same type pair, or an IncludeBase target is missing.</exception>
    public MapEngine(IEnumerable<MappingModule>? modules, IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
        _root = this;
        _holders = new();
        _explicitMapCache = new();
        Configuration = new FrozenConfiguration(modules);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MapEngine"/> from inline configuration, for example
    /// <c>new MapEngine(cfg =&gt; cfg.DefineMap&lt;Order, OrderDto&gt;())</c>.
    /// </summary>
    public MapEngine(Action<IMappingConfigurationBuilder> configure, IServiceProvider? serviceProvider = null)
        : this(new MappingConfiguration(configure).Modules, serviceProvider)
    {
    }

    private MapEngine(MapEngine root, IServiceProvider? serviceProvider)
    {
        _root = root;
        _serviceProvider = serviceProvider;
        _holders = root._holders;
        _explicitMapCache = root._explicitMapCache;
        Configuration = root.Configuration;
    }

    /// <summary>
    /// Returns an engine that shares this engine's configuration and compiled plans, but creates resolvers and
    /// converters from <paramref name="serviceProvider"/>. Creating one is cheap.
    /// </summary>
    /// <remarks>
    /// Dependency injection uses this to give each scope, such as an HTTP request, an engine that resolves scoped
    /// services from that scope, while the expensive compiled plans are built only once.
    /// </remarks>
    public IMapEngine WithServiceProvider(IServiceProvider? serviceProvider) => new MapEngine(_root, serviceProvider);

    internal FrozenConfiguration Configuration { get; }

    /// <summary>
    /// The engine that owns the compiled plans. A scoped view returns the engine it was created from.
    /// </summary>
    internal MapEngine Root => _root;

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source is null)
        {
            return default!;
        }

        return GetHolder<TSource, TDestination>().Map(source, default!, false, CreateContext());
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source is null)
        {
            return destination;
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination), "Existing destination object cannot be null.");
        }

        return GetHolder<TSource, TDestination>().Map(source, destination, true, CreateContext());
    }

    public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (source is null) return default!;

        var context = CreateContext(options, source, out var _);
        return GetHolder<TSource, TDestination>().Map(source, default!, false, context);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (source is null) return destination;
        if (destination is null) throw new ArgumentNullException(nameof(destination), "Existing destination object cannot be null.");

        var context = CreateContext(options, source, out var _);
        return GetHolder<TSource, TDestination>().Map(source, destination, true, context);
    }

    public TDestination Map<TDestination>(object source, Action<IMappingOperationOptions> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (source == null) return default!;

        var operation = new MappingOperationOptions<object, TDestination>();
        options(operation);
        var context = new ResolutionContext(this, _serviceProvider, operation.Items);
        return (TDestination)GetHolder(source.GetType(), typeof(TDestination)).MapObject(source, null, false, context)!;
    }

    private ResolutionContext CreateContext<TSource, TDestination>(
        Action<IMappingOperationOptions<TSource, TDestination>> configure, TSource source, out MappingOperationOptions<TSource, TDestination> operation)
    {
        operation = new MappingOperationOptions<TSource, TDestination>();
        configure(operation);
        var context = new ResolutionContext(this, _serviceProvider, operation.Items);

        var before = operation.Before;
        var after = operation.After;
        if (before != null || after != null)
        {
            context.SetOperationCallbacks(
                source!,
                before == null ? null : (s, d) => before((TSource)s, (TDestination)d),
                after == null ? null : (s, d) => after((TSource)s, (TDestination)d));
        }

        return context;
    }

    public TDestination Map<TDestination>(object source)
    {
        if (source == null)
        {
            return default!;
        }

        return (TDestination)GetHolder(source.GetType(), typeof(TDestination)).MapObject(source, null, false, CreateContext())!;
    }

    public object Map(object source, Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        if (source == null)
        {
            return destinationType.IsValueType ? Activator.CreateInstance(destinationType)! : null!;
        }

        return GetHolder(sourceType, destinationType).MapObject(source, null, false, CreateContext())!;
    }

    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        if (source == null)
        {
            return destination;
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination), "Existing destination instance cannot be null.");
        }

        return GetHolder(sourceType, destinationType).MapObject(source, destination, true, CreateContext())!;
    }

    private ResolutionContext CreateContext() => new(this, _serviceProvider);

    internal MapperHolder<TSource, TDestination> GetHolder<TSource, TDestination>()
    {
        // Most apps have one engine, so cache the last holder per type pair and skip the dictionary.
        var cached = HolderCache<TSource, TDestination>.Last;
        if (cached != null && ReferenceEquals(cached.Engine, _root))
        {
            return cached.Holder;
        }

        var holder = (MapperHolder<TSource, TDestination>)GetHolder(typeof(TSource), typeof(TDestination));
        HolderCache<TSource, TDestination>.Last = new HolderCache<TSource, TDestination>.Entry(_root, holder);
        return holder;
    }

    internal MapperHolder GetHolder(Type sourceType, Type destinationType)
    {
        return _holders.GetOrAdd((sourceType, destinationType), key =>
        {
            var map = Configuration.Find(key.Source, key.Destination);

            // Generated maps pass a closed generic type, so no MakeGenericType call (keeps Native AOT happy).
            return Generated.GeneratedMapRegistry.Find(key.Source, key.Destination)?.CreateHolder(_root, map)
                   ?? (MapperHolder)Activator.CreateInstance(typeof(MapperHolder<,>).MakeGenericType(key.Source, key.Destination), _root, map)!;
        });
    }

    /// <summary>
    /// Returns true when a map was explicitly defined from <paramref name="sourceType"/>, or one of its base types,
    /// to exactly <paramref name="destinationType"/>.
    /// </summary>
    internal bool HasExplicitMap(Type sourceType, Type destinationType)
    {
        return _explicitMapCache.GetOrAdd((sourceType, destinationType), key =>
            Configuration.Contains(key.Source, key.Destination) ||
            Configuration.Maps.Any(m => m.DestinationType == key.Destination && m.SourceType.IsAssignableFrom(key.Source)));
    }

    /// <summary>
    /// Returns the expression tree the engine compiles for a type pair, for debugging. Equivalent to AutoMapper's
    /// <c>BuildExecutionPlan</c>. Polymorphic dispatch, type converters, reference preservation and <c>MaxDepth</c>
    /// run around this plan and are not part of it.
    /// </summary>
    public LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        var build = typeof(PlanBuilder).GetMethod(nameof(PlanBuilder.Build))!.MakeGenericMethod(sourceType, destinationType);
        return (LambdaExpression)build.Invoke(null, new object?[] { _root, Configuration.Find(sourceType, destinationType) })!;
    }

    /// <summary>
    /// Describes how a type pair is mapped: by code from the source generator, or by a compiled or interpreted expression tree,
    /// with the reason generated code was not used.
    /// </summary>
    public Generated.MapPlanInfo GetPlanInfo(Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        var holder = GetHolder(sourceType, destinationType);
        holder.Compile();
        return holder.PlanInfo!;
    }

    /// <summary>
    /// Describes how every defined map runs: generated code, or a compiled or interpreted expression tree, with the reason
    /// generated code is not used. Maps that use a type converter or have an abstract destination are skipped.
    /// </summary>
    public IReadOnlyList<(Type Source, Type Destination, Generated.MapPlanInfo Plan)> DescribePlans()
    {
        var plans = new List<(Type, Type, Generated.MapPlanInfo)>();
        foreach (var map in Configuration.Maps)
        {
            if (map.CustomConverter != null || map.CustomConverterType != null || map.DestinationType.IsAbstract || map.DestinationType.IsInterface)
            {
                continue;
            }

            plans.Add((map.SourceType, map.DestinationType, GetPlanInfo(map.SourceType, map.DestinationType)));
        }

        return plans;
    }

    /// <summary>
    /// Compiles the plans of every defined map now instead of on first use, for example at application start-up.
    /// Equivalent to AutoMapper's <c>CompileMappings</c>.
    /// </summary>
    public void CompileMappings()
    {
        foreach (var map in Configuration.Maps)
        {
            if (map.CustomConverter == null && map.CustomConverterType == null && !map.DestinationType.IsAbstract && !map.DestinationType.IsInterface)
            {
                GetHolder(map.SourceType, map.DestinationType).Compile();
            }
        }
    }

    public void AssertConfigurationIsValid()
    {
        var result = ValidateConfiguration();
        if (!result.IsValid)
        {
            throw new AutoMapSharpConfigurationException(result.GenerateReport());
        }
    }

    /// <summary>
    /// Returns the effective, frozen configuration for a type pair, with module settings and IncludeBase rules applied.
    /// </summary>
    public TypeMappingDescriptor? FindDescriptor(Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));
        return Configuration.Find(sourceType, destinationType);
    }

    /// <summary>
    /// Validates every defined map with the same matching and conversion rules the engine uses: unmapped destination
    /// members, or unused source members for maps validated with <c>MemberList.Source</c>, member types that cannot be
    /// converted, constructors that cannot be satisfied, unknown <c>ForCtorParam</c> names, <c>Include</c> pairs without a map,
    /// and the automatic maps used for nested types.
    /// </summary>
    public ConfigurationValidationResult ValidateConfiguration() => new ConfigurationValidator(_root).Validate();

    private static class HolderCache<TSource, TDestination>
    {
        public static Entry? Last;

        public sealed class Entry
        {
            public Entry(MapEngine engine, MapperHolder<TSource, TDestination> holder)
            {
                Engine = engine;
                Holder = holder;
            }

            public MapEngine Engine { get; }

            public MapperHolder<TSource, TDestination> Holder { get; }
        }
    }
}
