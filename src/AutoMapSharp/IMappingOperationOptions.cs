using System;
using System.Collections.Generic;

namespace AutoMapSharp;

/// <summary>
/// Options for a single <c>Map</c> call. Equivalent to AutoMapper's <c>IMappingOperationOptions</c>.
/// </summary>
public interface IMappingOperationOptions
{
    /// <summary>
    /// Values available to resolvers, converters and callbacks through <see cref="ResolutionContext.Items"/> during this call.
    /// </summary>
    IDictionary<string, object?> Items { get; }
}

/// <summary>
/// Options for a single typed <c>Map</c> call.
/// </summary>
public interface IMappingOperationOptions<TSource, TDestination> : IMappingOperationOptions
{
    /// <summary>
    /// Runs before the members of the top-level object are mapped, after it is created.
    /// </summary>
    IMappingOperationOptions<TSource, TDestination> BeforeMap(Action<TSource, TDestination> callback);

    /// <summary>
    /// Runs after the top-level object is mapped.
    /// </summary>
    IMappingOperationOptions<TSource, TDestination> AfterMap(Action<TSource, TDestination> callback);
}

internal sealed class MappingOperationOptions<TSource, TDestination> : IMappingOperationOptions<TSource, TDestination>
{
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public Action<TSource, TDestination>? Before { get; private set; }

    public Action<TSource, TDestination>? After { get; private set; }

    public IMappingOperationOptions<TSource, TDestination> BeforeMap(Action<TSource, TDestination> callback)
    {
        Before += callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    public IMappingOperationOptions<TSource, TDestination> AfterMap(Action<TSource, TDestination> callback)
    {
        After += callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }
}
