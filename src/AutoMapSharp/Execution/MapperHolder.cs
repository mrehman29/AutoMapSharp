using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp.Execution;

/// <summary>
/// Owns the compiled plan for one source and destination type pair.
/// </summary>
internal abstract class MapperHolder
{
    public abstract Type SourceType { get; }

    public abstract Type DestinationType { get; }

    public abstract object? MapObject(object? source, object? destination, bool useDestination, ResolutionContext context);

    /// <summary>
    /// Compiles the plan now, if it is not compiled yet.
    /// </summary>
    public abstract void Compile();

    /// <summary>
    /// How this pair is mapped. Set when the plan is first built.
    /// </summary>
    public Generated.MapPlanInfo? PlanInfo { get; protected set; }
}

/// <summary>
/// Owns the compiled plan for <typeparamref name="TSource"/> to <typeparamref name="TDestination"/> and runs the steps
/// around it: polymorphic dispatch, type converters, reference preservation, maximum depth and error wrapping.
/// </summary>
/// <remarks>
/// Plans call the holders of nested type pairs directly, so a nested map costs one method call and no dictionary lookup.
/// A holder is created before its plan is compiled, which lets recursive types refer to their own holder.
/// </remarks>
internal sealed class MapperHolder<TSource, TDestination> : MapperHolder
{
    internal static readonly MethodInfo MapMethod = typeof(MapperHolder<TSource, TDestination>).GetMethod(nameof(Map))!;

    private readonly MapEngine _engine;
    private readonly TypeMappingDescriptor? _map;
    private readonly Lazy<Func<TSource, TDestination, bool, ResolutionContext, TDestination>> _plan;
    private readonly Lazy<Func<TSource, ResolutionContext, TDestination>?> _converter;
    private readonly Lazy<MapperHolder?> _selfRedirect;
    private readonly ConcurrentDictionary<Type, MapperHolder?> _dispatch = new();
    private readonly bool _canDispatch;

    public MapperHolder(MapEngine engine, TypeMappingDescriptor? map)
    {
        _engine = engine;
        _map = map;
        _plan = new Lazy<Func<TSource, TDestination, bool, ResolutionContext, TDestination>>(
            () => BuildPlan(engine, map), LazyThreadSafetyMode.ExecutionAndPublication);
        _converter = new Lazy<Func<TSource, ResolutionContext, TDestination>?>(BuildConverter, LazyThreadSafetyMode.ExecutionAndPublication);
        _selfRedirect = new Lazy<MapperHolder?>(() => ResolveDispatch(typeof(TSource)), LazyThreadSafetyMode.ExecutionAndPublication);
        _canDispatch = !typeof(TSource).IsValueType && !typeof(TSource).IsSealed;
    }

    public override Type SourceType => typeof(TSource);

    public override Type DestinationType => typeof(TDestination);

    public TDestination Map(TSource source, TDestination destination, bool useDestination, ResolutionContext context)
    {
        if (source is null)
        {
            return useDestination ? destination : default!;
        }

        // Polymorphism: a more specific map for the runtime type, an Include, or a concrete map for an abstract destination.
        if (_canDispatch)
        {
            var runtimeType = source.GetType();
            var target = runtimeType == typeof(TSource) ? _selfRedirect.Value : _dispatch.GetOrAdd(runtimeType, ResolveDispatch);
            if (target != null)
            {
                return (TDestination)target.MapObject(source, destination, useDestination, context)!;
            }
        }
        else if (_selfRedirect.Value is { } redirect)
        {
            return (TDestination)redirect.MapObject(source, destination, useDestination, context)!;
        }

        try
        {
            var converter = _converter.Value;
            if (converter != null)
            {
                var converted = converter(source, context);
                if (_map!.PreserveReferences && converted is not null)
                {
                    context.RegisterMappedInstance(source, typeof(TDestination), converted);
                }

                return converted;
            }

            if (_map != null && _map.PreserveReferences && context.TryGetMappedInstance(source, typeof(TDestination), out var existing))
            {
                return (TDestination)existing!;
            }

            if (_map?.MaxDepth is int maxDepth)
            {
                var depth = context.IncrementDepth(typeof(TSource), typeof(TDestination));
                try
                {
                    return depth > maxDepth ? default! : _plan.Value(source, destination, useDestination, context);
                }
                finally
                {
                    context.DecrementDepth(typeof(TSource), typeof(TDestination));
                }
            }

            return _plan.Value(source, destination, useDestination, context);
        }
        catch (Exception ex) when (ex is not AutoMapSharpException)
        {
            throw new AutoMapSharpMappingException(
                $"Error occurred while mapping from '{typeof(TSource).FullName}' to '{typeof(TDestination).FullName}': {ex.Message}",
                typeof(TSource), typeof(TDestination), ex);
        }
    }

    public override void Compile() => _ = _plan.Value;

    /// <summary>
    /// Uses the code emitted by the source generator when it matches the runtime configuration, otherwise compiles an expression tree.
    /// </summary>
    private Func<TSource, TDestination, bool, ResolutionContext, TDestination> BuildPlan(MapEngine engine, TypeMappingDescriptor? map)
    {
        var runtimeKind = PlanCompiler.Interpreted ? Generated.MapPlanKind.Interpreted : Generated.MapPlanKind.Compiled;
        var generated = Generated.GeneratedMapRegistry.Find<TSource, TDestination>();
        if (generated != null)
        {
            var mismatch = generated.FindMismatch(engine, map);
            if (mismatch == null)
            {
                PlanInfo = new Generated.MapPlanInfo(Generated.MapPlanKind.Generated, null);
                return generated.Bind(map);
            }

            PlanInfo = new Generated.MapPlanInfo(runtimeKind, "generated code was not used because " + mismatch);
        }
        else
        {
            PlanInfo = new Generated.MapPlanInfo(runtimeKind, Generated.GeneratedMapRegistry.Disabled
                ? "generated code is disabled by AUTOMAPSHARP_DISABLE_GENERATED"
                : "the source generator did not emit code for this pair");
        }

#if NETSTANDARD2_0
        return PlanCompiler.Compile(PlanBuilder.Build<TSource, TDestination>(engine, map));
#else
        try
        {
            return PlanCompiler.Compile(PlanBuilder.Build<TSource, TDestination>(engine, map));
        }
        catch (Exception ex) when (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported && ex is not AutoMapSharpException)
        {
            throw new AutoMapSharpConfigurationException(
                $"The map from '{typeof(TSource).FullName}' to '{typeof(TDestination).FullName}' cannot be built on this platform, which does not support dynamic code, " +
                $"for example Native AOT. Only maps emitted by the AutoMapSharp source generator are supported there, and {PlanInfo!.Reason}. " +
                "See the AMS100 build messages for why a map is not generated.", ex);
        }
#endif
    }

    public override object? MapObject(object? source, object? destination, bool useDestination, ResolutionContext context)
    {
        if (source == null)
        {
            return useDestination ? destination : null;
        }

        var typedDestination = destination is TDestination d ? d : default!;
        return Map((TSource)source!, typedDestination, useDestination && destination != null, context);
    }

    /// <summary>
    /// Finds the map to use instead of this one for a source object of <paramref name="runtimeType"/>, or null to use this one.
    /// </summary>
    private MapperHolder? ResolveDispatch(Type runtimeType)
    {
        var config = _engine.Configuration;

        if (runtimeType != typeof(TSource))
        {
            if (_map != null)
            {
                foreach (var (derivedSource, derivedDestination) in _map.IncludedDerivedTypes)
                {
                    if (derivedSource.IsAssignableFrom(runtimeType) && typeof(TDestination).IsAssignableFrom(derivedDestination))
                    {
                        return _engine.GetHolder(derivedSource, derivedDestination);
                    }
                }
            }

            if (config.Contains(runtimeType, typeof(TDestination)))
            {
                return _engine.GetHolder(runtimeType, typeof(TDestination));
            }

            // IncludeAllDerived: the defined map whose source type is closest to the runtime type.
            if (_map?.IncludeAllDerivedTypes == true)
            {
                var closest = config.Maps
                    .Where(m => m.SourceType != typeof(TSource) &&
                                m.SourceType.IsAssignableFrom(runtimeType) &&
                                typeof(TDestination).IsAssignableFrom(m.DestinationType) &&
                                !m.DestinationType.IsAbstract && !m.DestinationType.IsInterface)
                    .OrderByDescending(m => Depth(m.SourceType))
                    .ThenByDescending(m => Depth(m.DestinationType))
                    .FirstOrDefault();
                if (closest != null)
                {
                    return _engine.GetHolder(closest.SourceType, closest.DestinationType);
                }
            }
        }

        // As<TOther>(): map to the more specific destination type instead.
        if (_map?.AsType is { } asType)
        {
            return _engine.GetHolder(typeof(TSource), asType);
        }

        // An abstract destination, or a pair with no map of its own: look for a configured map that fits.
        if (_map == null || typeof(TDestination).IsAbstract || typeof(TDestination).IsInterface)
        {
            var candidate = config.Maps.FirstOrDefault(m =>
                m.SourceType.IsAssignableFrom(runtimeType) &&
                typeof(TDestination).IsAssignableFrom(m.DestinationType) &&
                !m.DestinationType.IsAbstract &&
                !m.DestinationType.IsInterface &&
                !(m.SourceType == typeof(TSource) && m.DestinationType == typeof(TDestination)));

            if (candidate != null)
            {
                return _engine.GetHolder(candidate.SourceType, candidate.DestinationType);
            }
        }

        return null;
    }

    private static int Depth(Type type)
    {
        var depth = 0;
        for (var current = type.BaseType; current != null; current = current.BaseType) depth++;
        return depth;
    }

    private Func<TSource, ResolutionContext, TDestination>? BuildConverter()
    {
        if (_map == null)
        {
            return null;
        }

        if (_map.CustomConverter is Func<TSource, ResolutionContext, TDestination> function)
        {
            return function;
        }

        if (_map.CustomConverterType is not { } converterType)
        {
            return null;
        }

        var converterInterface = converterType.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(ITypeConverter<,>) &&
            i.GetGenericArguments()[0].IsAssignableFrom(typeof(TSource)) &&
            typeof(TDestination).IsAssignableFrom(i.GetGenericArguments()[1]))
            ?? throw new AutoMapSharpConfigurationException(
                $"Converter type '{converterType.FullName}' does not implement ITypeConverter<{typeof(TSource).Name}, {typeof(TDestination).Name}>.");

        var factory = new InstanceFactory(converterType);
        var source = Expression.Parameter(typeof(TSource), "source");
        var context = Expression.Parameter(typeof(ResolutionContext), "context");
        var instance = Expression.Convert(factory.CreateExpression(context), converterInterface);
        var call = Expression.Call(instance, converterInterface.GetMethod("Convert")!, source, context);
        return PlanCompiler.Compile(Expression.Lambda<Func<TSource, ResolutionContext, TDestination>>(Expression.Convert(call, typeof(TDestination)), source, context));
    }
}

/// <summary>
/// Creates resolver and converter instances: from the service provider when one is registered there,
/// otherwise with a compiled call to the parameterless constructor.
/// </summary>
internal sealed class InstanceFactory
{
    private static readonly MethodInfo CreateMethod = typeof(InstanceFactory).GetMethod(nameof(Create))!;

    private readonly Type _type;
    private readonly Func<object>? _construct;
    private readonly ConstructorInfo[] _constructors;

    public InstanceFactory(Type type)
    {
        _type = type;
        _constructors = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).ToArray();
        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
        {
            _construct = PlanCompiler.Compile(Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(ctor), typeof(object))));
        }
    }

    public Expression CreateExpression(Expression context) => Expression.Call(Expression.Constant(this), CreateMethod, context);

    public object Create(ResolutionContext context)
    {
        var fromServices = context.ServiceProvider?.GetService(_type);
        if (fromServices != null)
        {
            return fromServices;
        }

        if (_construct != null)
        {
            return _construct();
        }

        // Not registered and no parameterless constructor: inject constructor parameters from the service provider.
        if (context.ServiceProvider is { } services)
        {
            foreach (var ctor in _constructors)
            {
                var parameters = ctor.GetParameters();
                var arguments = new object?[parameters.Length];
                var resolved = true;
                for (int i = 0; i < parameters.Length && resolved; i++)
                {
                    arguments[i] = services.GetService(parameters[i].ParameterType);
                    resolved = arguments[i] != null || parameters[i].HasDefaultValue;
                    if (arguments[i] == null && parameters[i].HasDefaultValue)
                    {
                        arguments[i] = parameters[i].DefaultValue;
                    }
                }

                if (resolved)
                {
                    return ctor.Invoke(arguments);
                }
            }
        }

        throw new AutoMapSharpConfigurationException(
            $"Cannot create '{_type.FullName}'. Register it or its constructor dependencies in the service provider passed to the engine, " +
            "or give it a public parameterless constructor.");
    }
}
