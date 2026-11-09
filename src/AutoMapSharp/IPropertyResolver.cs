using System;

namespace AutoMapSharp;

/// <summary>
/// Defines a custom value resolver for calculating and mapping individual destination properties.
/// Resolvers can be resolved from the dependency injection container (<see cref="IServiceProvider"/>).
/// </summary>
/// <typeparam name="TSource">The source object type.</typeparam>
/// <typeparam name="TDestination">The destination object type.</typeparam>
/// <typeparam name="TDestMember">The type of the destination member being resolved.</typeparam>
public interface IPropertyResolver<in TSource, in TDestination, out TDestMember>
{
    /// <summary>
    /// Resolves the destination property value based on the source, destination, and context.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="destination">The destination instance being mapped onto.</param>
    /// <param name="context">The active resolution context.</param>
    /// <returns>The resolved destination member value.</returns>
    TDestMember Resolve(TSource source, TDestination destination, ResolutionContext context);
}
