using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoMapSharp.Extensions.DependencyInjection;

/// <summary>
/// Options for <see cref="AutoMapSharpServiceCollectionExtensions.AddAutoMapSharp(IServiceCollection, Action{AutoMapSharpOptions}, Assembly[])"/>.
/// </summary>
public sealed class AutoMapSharpOptions
{
    internal List<Type> ModuleTypes { get; } = new();
    internal List<MappingModule> ModuleInstances { get; } = new();
    internal List<Assembly> Assemblies { get; } = new();

    /// <summary>
    /// Adds a module type. It is created by the container, so it can take constructor dependencies.
    /// </summary>
    public AutoMapSharpOptions AddModule<TModule>() where TModule : MappingModule
    {
        ModuleTypes.Add(typeof(TModule));
        return this;
    }

    /// <summary>
    /// Adds an existing module instance.
    /// </summary>
    public AutoMapSharpOptions AddModule(MappingModule module)
    {
        ModuleInstances.Add(module ?? throw new ArgumentNullException(nameof(module)));
        return this;
    }

    /// <summary>
    /// Adds maps defined inline, for example <c>AddMaps(cfg =&gt; cfg.DefineMap&lt;Order, OrderDto&gt;())</c>.
    /// </summary>
    public AutoMapSharpOptions AddMaps(Action<IMappingConfigurationBuilder> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        ModuleInstances.AddRange(new MappingConfiguration(configure).Modules);
        return this;
    }

    /// <summary>
    /// Scans an assembly for modules, resolvers and converters.
    /// </summary>
    public AutoMapSharpOptions AddAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly ?? throw new ArgumentNullException(nameof(assembly)));
        return this;
    }
}

/// <summary>
/// Extension methods for configuring AutoMapSharp with Microsoft.Extensions.DependencyInjection.
/// </summary>
/// <remarks>
/// The compiled configuration is a singleton, so plans are built once per application. <see cref="IMapEngine"/> is
/// registered as transient: each one is a cheap view that creates resolvers and converters from the scope it was
/// resolved from, so a resolver can depend on a scoped service such as a DbContext.
/// </remarks>
public static class AutoMapSharpServiceCollectionExtensions
{
    /// <summary>
    /// Scans the assemblies for <see cref="MappingModule"/> classes, property resolvers, type converters, value converters and mapping actions,
    /// public and internal, and registers <see cref="IMapEngine"/>. Private nested classes are skipped.
    /// </summary>
    public static IServiceCollection AddAutoMapSharp(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (assemblies == null || assemblies.Length == 0)
        {
            throw new ArgumentException(
                "Pass at least one assembly to scan, for example AddAutoMapSharp(typeof(Program).Assembly).", nameof(assemblies));
        }

        return services.AddAutoMapSharp(options =>
        {
            foreach (var assembly in assemblies)
            {
                options.AddAssembly(assembly);
            }
        });
    }

    /// <summary>
    /// Registers the given types and <see cref="IMapEngine"/>. A <see cref="MappingModule"/> type is registered as a module.
    /// Any other type marks its assembly for scanning, like <c>AddAutoMapper(typeof(Program))</c>.
    /// </summary>
    public static IServiceCollection AddAutoMapSharp(this IServiceCollection services, params Type[] moduleOrMarkerTypes)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (moduleOrMarkerTypes == null || moduleOrMarkerTypes.Length == 0)
        {
            throw new ArgumentException("Pass at least one module type or marker type.", nameof(moduleOrMarkerTypes));
        }

        return services.AddAutoMapSharp(options =>
        {
            foreach (var type in moduleOrMarkerTypes)
            {
                if (type == null) throw new ArgumentNullException(nameof(moduleOrMarkerTypes));

                if (typeof(MappingModule).IsAssignableFrom(type))
                {
                    if (type.IsAbstract)
                    {
                        throw new ArgumentException($"Module type '{type.FullName}' is abstract.", nameof(moduleOrMarkerTypes));
                    }

                    options.ModuleTypes.Add(type);
                }
                else
                {
                    options.AddAssembly(type.Assembly);
                }
            }
        });
    }

    /// <summary>
    /// Registers AutoMapSharp with explicit options, plus any assemblies to scan.
    /// </summary>
    public static IServiceCollection AddAutoMapSharp(this IServiceCollection services, Action<AutoMapSharpOptions> configure, params Assembly[] assemblies)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var options = new AutoMapSharpOptions();
        configure(options);
        options.Assemblies.AddRange(assemblies ?? Array.Empty<Assembly>());

        var attributeAssemblies = options.Assemblies.Distinct().Where(AttributeMappingModule.HasAttributeMaps).ToArray();
        if (attributeAssemblies.Length > 0)
        {
            options.ModuleInstances.Add(new AttributeMappingModule(attributeAssemblies));
        }

        foreach (var assembly in options.Assemblies.Distinct())
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                // Private nested types are local helpers, such as a module a controller uses for its own engine.
                if (type == typeof(AttributeMappingModule) || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition ||
                    type.IsNestedPrivate || type.IsNestedFamily || type.IsNestedFamANDAssem)
                {
                    continue;
                }

                if (typeof(MappingModule).IsAssignableFrom(type))
                {
                    options.ModuleTypes.Add(type);
                }
                else if (ImplementsGeneric(type, typeof(IPropertyResolver<,,>)) || ImplementsGeneric(type, typeof(ITypeConverter<,>)) ||
                         ImplementsGeneric(type, typeof(IValueConverter<,>)) || ImplementsGeneric(type, typeof(IMappingAction<,>)))
                {
                    services.TryAddTransient(type);
                }
            }
        }

        foreach (var moduleType in options.ModuleTypes.Distinct())
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(MappingModule), moduleType));
        }

        foreach (var module in options.ModuleInstances)
        {
            services.AddSingleton(typeof(MappingModule), module);
        }

        services.TryAddSingleton(sp => new SharedMapEngine(new MapEngine(sp.GetServices<MappingModule>(), sp)));
        services.TryAddTransient<IMapEngine>(sp => sp.GetRequiredService<SharedMapEngine>().Engine.WithServiceProvider(sp));

        return services;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static bool ImplementsGeneric(Type type, Type definition) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);

    /// <summary>
    /// Holds the application-wide engine that owns the compiled plans.
    /// </summary>
    internal sealed class SharedMapEngine
    {
        public SharedMapEngine(MapEngine engine)
        {
            Engine = engine;
        }

        public MapEngine Engine { get; }
    }
}
