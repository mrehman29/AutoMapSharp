using System;
using System.Collections.Generic;

namespace AutoMapSharp.Configuration;

/// <summary>
/// Manages global and type-level transformation functions applied to property values during mapping.
/// Equivalent to AutoMapper's ValueTransformers collection.
/// </summary>
public sealed class ValueTransformerCollection
{
    private readonly Dictionary<Type, List<Delegate>> _transformers = new();

    /// <summary>
    /// Adds a value transformation delegate for all properties of type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The member type to transform.</typeparam>
    /// <param name="transformer">The transformation function.</param>
    public void Add<TValue>(Func<TValue, TValue> transformer)
    {
        if (transformer == null) throw new ArgumentNullException(nameof(transformer));

        var type = typeof(TValue);
        if (!_transformers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _transformers[type] = list;
        }

        list.Add(transformer);
    }

    /// <summary>
    /// Retrieves all registered transformer delegates for a specified type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>A list of transformer delegates, or empty if none registered.</returns>
    public IReadOnlyList<Delegate> GetTransformers(Type type)
    {
        if (type != null && _transformers.TryGetValue(type, out var list))
        {
            return list;
        }

        return Array.Empty<Delegate>();
    }

    /// <summary>
    /// Applies all registered transformers for <paramref name="targetType"/> to <paramref name="value"/>.
    /// </summary>
    public object? Apply(Type targetType, object? value)
    {
        if (targetType == null) return value;

        if (_transformers.TryGetValue(targetType, out var list))
        {
            var current = value;
            foreach (var transformer in list)
            {
                current = transformer.DynamicInvoke(current);
            }
            return current;
        }

        return value;
    }

    /// <summary>
    /// Copies transformers from another collection.
    /// </summary>
    internal void CopyFrom(ValueTransformerCollection source)
    {
        if (source == null) return;

        foreach (var kvp in source._transformers)
        {
            if (!_transformers.TryGetValue(kvp.Key, out var list))
            {
                list = new List<Delegate>();
                _transformers[kvp.Key] = list;
            }

            list.AddRange(kvp.Value);
        }
    }
}
