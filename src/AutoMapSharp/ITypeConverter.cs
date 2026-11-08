using System;

namespace AutoMapSharp;

/// <summary>
/// Defines a custom converter that takes complete ownership of converting a source instance
/// into a destination instance, bypassing member-by-member property generation.
/// </summary>
/// <typeparam name="TSource">The source object type.</typeparam>
/// <typeparam name="TDestination">The destination object type.</typeparam>
public interface ITypeConverter<in TSource, out TDestination>
{
    /// <summary>
    /// Converts the source instance into the destination instance.
    /// </summary>
    /// <param name="source">The source instance to convert.</param>
    /// <param name="context">The active resolution context.</param>
    /// <returns>The newly created destination instance.</returns>
    TDestination Convert(TSource source, ResolutionContext context);
}
