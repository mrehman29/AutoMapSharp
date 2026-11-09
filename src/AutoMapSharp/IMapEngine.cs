using System;

namespace AutoMapSharp;

/// <summary>
/// Executes object-to-object mapping operations.
/// </summary>
public interface IMapEngine
{
    /// <summary>
    /// Maps the source object to a newly instantiated <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">The compile-time source type.</typeparam>
    /// <typeparam name="TDestination">The target destination type.</typeparam>
    /// <param name="source">The source instance to map from.</param>
    /// <returns>The mapped destination instance.</returns>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Maps the source object onto an existing <paramref name="destination"/> instance.
    /// </summary>
    /// <typeparam name="TSource">The compile-time source type.</typeparam>
    /// <typeparam name="TDestination">The target destination type.</typeparam>
    /// <param name="source">The source instance to map from.</param>
    /// <param name="destination">The existing destination instance to update.</param>
    /// <returns>The updated destination instance.</returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    /// <summary>
    /// Maps the source object with per-call options such as <see cref="IMappingOperationOptions.Items"/>, BeforeMap and AfterMap.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> options);

    /// <summary>
    /// Maps the source object onto an existing destination with per-call options.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> options);

    /// <summary>
    /// Maps an untyped source object with per-call options.
    /// </summary>
    TDestination Map<TDestination>(object source, Action<IMappingOperationOptions> options);

    /// <summary>
    /// Maps an untyped source object to a newly instantiated <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TDestination">The target destination type.</typeparam>
    /// <param name="source">The source instance to map from.</param>
    /// <returns>The mapped destination instance.</returns>
    TDestination Map<TDestination>(object source);

    /// <summary>
    /// Maps the source object to a newly instantiated object of <paramref name="destinationType"/>.
    /// </summary>
    /// <param name="source">The source instance to map from.</param>
    /// <param name="sourceType">The runtime type of the source object.</param>
    /// <param name="destinationType">The runtime type of the destination object.</param>
    /// <returns>The mapped destination instance.</returns>
    object Map(object source, Type sourceType, Type destinationType);

    /// <summary>
    /// Maps the source object onto an existing <paramref name="destination"/> instance of <paramref name="destinationType"/>.
    /// </summary>
    /// <param name="source">The source instance to map from.</param>
    /// <param name="destination">The existing destination instance to update.</param>
    /// <param name="sourceType">The runtime type of the source object.</param>
    /// <param name="destinationType">The runtime type of the destination object.</param>
    /// <returns>The updated destination instance.</returns>
    object Map(object source, object destination, Type sourceType, Type destinationType);

    /// <summary>
    /// Validates all registered mapping configurations, ensuring all destination members are mapped or explicitly ignored.
    /// Throws <see cref="Exceptions.AutoMapSharpConfigurationException"/> if any unmapped properties or conflicts exist.
    /// </summary>
    void AssertConfigurationIsValid();

    /// <summary>
    /// Performs validation of all registered mappings and returns a detailed report without throwing.
    /// </summary>
    /// <returns>A <see cref="Validation.ConfigurationValidationResult"/> containing any validation errors.</returns>
    Validation.ConfigurationValidationResult ValidateConfiguration();

    /// <summary>
    /// Returns the expression tree compiled for a type pair, for debugging.
    /// </summary>
    System.Linq.Expressions.LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType);

    /// <summary>
    /// Retrieves the registered mapping descriptor for a given source and destination type pair, if configured.
    /// </summary>
    Configuration.TypeMappingDescriptor? FindDescriptor(Type sourceType, Type destinationType);
}
