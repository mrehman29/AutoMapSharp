using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapSharp.Configuration;
using AutoMapSharp.Conventions;
using AutoMapSharp.Validation;

namespace AutoMapSharp;

/// <summary>
/// Configures maps inline, for example <c>cfg.DefineMap&lt;Order, OrderDto&gt;()</c>, and adds modules.
/// </summary>
public interface IMappingConfigurationBuilder
{
    /// <summary>
    /// Defines a map inline.
    /// </summary>
    IMappingDefinition<TSource, TDestination> DefineMap<TSource, TDestination>();

    /// <summary>
    /// Adds a module with a parameterless constructor.
    /// </summary>
    IMappingConfigurationBuilder AddModule<TModule>() where TModule : MappingModule, new();

    /// <summary>
    /// Adds an existing module instance.
    /// </summary>
    IMappingConfigurationBuilder AddModule(MappingModule module);

    /// <summary>
    /// Adds every public, non-abstract module with a parameterless constructor from an assembly.
    /// </summary>
    IMappingConfigurationBuilder AddModules(Assembly assembly);

    /// <summary>
    /// Adds the maps defined with <see cref="MapFromAttribute"/> in an assembly.
    /// </summary>
    IMappingConfigurationBuilder AddAttributeMaps(Assembly assembly);

    /// <summary>
    /// Value transformers for the inline maps.
    /// </summary>
    ValueTransformerCollection ValueTransformers { get; }

    /// <summary>
    /// The source naming convention for the inline maps.
    /// </summary>
    INamingConvention SourceNamingConvention { get; set; }

    /// <summary>
    /// The destination naming convention for the inline maps.
    /// </summary>
    INamingConvention DestinationNamingConvention { get; set; }

    /// <summary>
    /// Whether null source collections give null rather than empty collections in the inline maps.
    /// </summary>
    bool AllowNullCollections { get; set; }

    /// <summary>
    /// Recognizes source member prefixes for the inline maps.
    /// </summary>
    IMappingConfigurationBuilder RecognizePrefixes(params string[] prefixes);

    /// <summary>
    /// Recognizes source member suffixes for the inline maps.
    /// </summary>
    IMappingConfigurationBuilder RecognizeSuffixes(params string[] suffixes);
}

/// <summary>
/// Configuration built inline, without writing a module class. Equivalent to AutoMapper's <c>MapperConfiguration</c>.
/// </summary>
/// <example>
/// <code>
/// var config = new MappingConfiguration(cfg =>
/// {
///     cfg.DefineMap&lt;Order, OrderDto&gt;();
///     cfg.AddModule&lt;CustomerModule&gt;();
/// });
/// config.AssertConfigurationIsValid();
/// var engine = config.CreateEngine();
/// </code>
/// </example>
public sealed class MappingConfiguration
{
    private readonly List<MappingModule> _modules;

    public MappingConfiguration(Action<IMappingConfigurationBuilder> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var inline = new InlineMappingModule();
        configure(inline);
        _modules = new List<MappingModule> { inline };
        _modules.AddRange(inline.AddedModules);
        _engine = new Lazy<MapEngine>(() => new MapEngine(_modules));
    }

    private readonly Lazy<MapEngine> _engine;

    /// <summary>
    /// The modules this configuration contains, starting with the one that holds the inline maps.
    /// </summary>
    public IReadOnlyList<MappingModule> Modules => _modules;

    internal MapEngine Engine => _engine.Value;

    /// <summary>
    /// Returns an engine for this configuration. Engines created from one configuration share compiled plans.
    /// </summary>
    public IMapEngine CreateEngine(IServiceProvider? serviceProvider = null) => Engine.WithServiceProvider(serviceProvider);

    /// <summary>
    /// Throws an <see cref="Exceptions.AutoMapSharpConfigurationException"/> with a report when the configuration is invalid.
    /// </summary>
    public void AssertConfigurationIsValid() => Engine.AssertConfigurationIsValid();

    /// <summary>
    /// Returns the expression tree compiled for a type pair, for debugging.
    /// </summary>
    public System.Linq.Expressions.LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType) => Engine.BuildExecutionPlan(sourceType, destinationType);

    /// <summary>
    /// Compiles every map now instead of on first use.
    /// </summary>
    public void CompileMappings() => Engine.CompileMappings();

    /// <summary>
    /// Validates the configuration and returns a report without throwing.
    /// </summary>
    public ConfigurationValidationResult ValidateConfiguration() => Engine.ValidateConfiguration();
}

/// <summary>
/// The module behind inline configuration.
/// </summary>
internal sealed class InlineMappingModule : MappingModule, IMappingConfigurationBuilder
{
    internal List<MappingModule> AddedModules { get; } = new();

    IMappingDefinition<TSource, TDestination> IMappingConfigurationBuilder.DefineMap<TSource, TDestination>() => DefineMap<TSource, TDestination>();

    public IMappingConfigurationBuilder AddModule<TModule>() where TModule : MappingModule, new()
    {
        AddedModules.Add(new TModule());
        return this;
    }

    public IMappingConfigurationBuilder AddModule(MappingModule module)
    {
        AddedModules.Add(module ?? throw new ArgumentNullException(nameof(module)));
        return this;
    }

    public IMappingConfigurationBuilder AddModules(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        foreach (var type in assembly.GetExportedTypes().Where(t =>
                     !t.IsAbstract && typeof(MappingModule).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null))
        {
            AddedModules.Add((MappingModule)Activator.CreateInstance(type)!);
        }

        return this;
    }

    public IMappingConfigurationBuilder AddAttributeMaps(Assembly assembly)
    {
        AddedModules.Add(new AttributeMappingModule(assembly ?? throw new ArgumentNullException(nameof(assembly))));
        return this;
    }

    INamingConvention IMappingConfigurationBuilder.SourceNamingConvention
    {
        get => SourceNamingConvention;
        set => SourceNamingConvention = value ?? throw new ArgumentNullException(nameof(value));
    }

    INamingConvention IMappingConfigurationBuilder.DestinationNamingConvention
    {
        get => DestinationNamingConvention;
        set => DestinationNamingConvention = value ?? throw new ArgumentNullException(nameof(value));
    }

    bool IMappingConfigurationBuilder.AllowNullCollections
    {
        get => AllowNullCollections;
        set => AllowNullCollections = value;
    }

    IMappingConfigurationBuilder IMappingConfigurationBuilder.RecognizePrefixes(params string[] prefixes)
    {
        RecognizePrefixes(prefixes);
        return this;
    }

    IMappingConfigurationBuilder IMappingConfigurationBuilder.RecognizeSuffixes(params string[] suffixes)
    {
        RecognizeSuffixes(suffixes);
        return this;
    }
}
