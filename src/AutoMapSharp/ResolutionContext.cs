using System;
using System.Collections.Generic;
using AutoMapSharp.Context;

namespace AutoMapSharp;

/// <summary>
/// Provides contextual information during a mapping operation, including the active engine,
/// service provider, reference preservation, recursion depth tracking, and custom user state.
/// </summary>
/// <remarks>
/// One context is created for each top-level <c>Map</c> call and shared by every nested map in that call.
/// Internal dictionaries are created only when a feature needs them, so a plain map allocates very little.
/// </remarks>
public sealed class ResolutionContext
{
    private Dictionary<ReferenceKey, object>? _references;
    private Dictionary<(Type, Type), int>? _depths;
    private IDictionary<string, object?>? _items;
    private object? _operationSource;
    private Action<object, object>? _operationBefore;
    private Action<object, object>? _operationAfter;

    /// <summary>
    /// Gets the current mapping engine executing the mapping.
    /// </summary>
    public IMapEngine Engine { get; }

    /// <summary>
    /// Gets the service provider for dependency-injected resolvers and converters, if configured.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; }

    /// <summary>
    /// Gets a dictionary of custom items passed across the mapping operation.
    /// </summary>
    public IDictionary<string, object?> Items => _items ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of <see cref="ResolutionContext"/>.
    /// </summary>
    public ResolutionContext(IMapEngine engine, IServiceProvider? serviceProvider = null, IDictionary<string, object?>? items = null)
    {
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        ServiceProvider = serviceProvider;
        _items = items;
    }

    /// <summary>
    /// True when the current Map call has per-call BeforeMap or AfterMap callbacks.
    /// </summary>
    internal bool HasOperationCallbacks;

    internal void SetOperationCallbacks(object source, Action<object, object>? before, Action<object, object>? after)
    {
        _operationSource = source;
        _operationBefore = before;
        _operationAfter = after;
        HasOperationCallbacks = before != null || after != null;
    }

    /// <summary>
    /// Runs the per-call BeforeMap callback when <paramref name="source"/> is the top-level source object.
    /// </summary>
    internal void OnBeforeMap(object source, object destination)
    {
        if (ReferenceEquals(source, _operationSource) && _operationBefore != null)
        {
            var callback = _operationBefore;
            _operationBefore = null;
            callback(source, destination);
        }
    }

    /// <summary>
    /// Runs the per-call AfterMap callback when <paramref name="source"/> is the top-level source object.
    /// </summary>
    internal void OnAfterMap(object source, object destination)
    {
        if (ReferenceEquals(source, _operationSource) && _operationAfter != null)
        {
            var callback = _operationAfter;
            _operationAfter = null;
            callback(source, destination);
        }
    }

    /// <summary>
    /// Attempts to retrieve the destination instance already created for <paramref name="source"/>
    /// as a <paramref name="destinationType"/> during this mapping operation.
    /// </summary>
    public bool TryGetMappedInstance(object source, Type destinationType, out object? destination)
    {
        if (source != null && _references != null && _references.TryGetValue(new ReferenceKey(source, destinationType), out var found))
        {
            destination = found;
            return true;
        }

        destination = null;
        return false;
    }

    /// <summary>
    /// Registers the destination instance created for <paramref name="source"/> as a <paramref name="destinationType"/>.
    /// </summary>
    public void RegisterMappedInstance(object source, Type destinationType, object destination)
    {
        if (source != null && destination != null)
        {
            (_references ??= new Dictionary<ReferenceKey, object>())[new ReferenceKey(source, destinationType)] = destination;
        }
    }

    /// <summary>
    /// Attempts to retrieve an already mapped destination instance, ignoring the destination type.
    /// </summary>
    [Obsolete("Use TryGetMappedInstance(object, Type, out object?). One source object can be mapped to several destination types.")]
    public bool TryGetMappedInstance(object source, out object? destination) => TryGetMappedInstance(source, typeof(object), out destination);

    /// <summary>
    /// Registers an instantiated destination instance, ignoring the destination type.
    /// </summary>
    [Obsolete("Use RegisterMappedInstance(object, Type, object). One source object can be mapped to several destination types.")]
    public void RegisterMappedInstance(object source, object destination) => RegisterMappedInstance(source, typeof(object), destination);

    /// <summary>
    /// Increments and returns the current recursion depth for a type pair.
    /// </summary>
    public int IncrementDepth(Type source, Type destination)
    {
        _depths ??= new Dictionary<(Type, Type), int>();
        var key = (source, destination);
        _depths.TryGetValue(key, out var current);
        current++;
        _depths[key] = current;
        return current;
    }

    /// <summary>
    /// Decrements the current recursion depth for a type pair.
    /// </summary>
    public void DecrementDepth(Type source, Type destination)
    {
        if (_depths == null) return;
        var key = (source, destination);
        if (_depths.TryGetValue(key, out var current))
        {
            if (current <= 1)
            {
                _depths.Remove(key);
            }
            else
            {
                _depths[key] = current - 1;
            }
        }
    }

    private readonly struct ReferenceKey : IEquatable<ReferenceKey>
    {
        private readonly object _source;
        private readonly Type _destinationType;

        public ReferenceKey(object source, Type destinationType)
        {
            _source = source;
            _destinationType = destinationType;
        }

        public bool Equals(ReferenceKey other) => ReferenceEquals(_source, other._source) && _destinationType == other._destinationType;

        public override bool Equals(object? obj) => obj is ReferenceKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (AutoMapSharpReferenceEqualityComparer.Instance.GetHashCode(_source) * 397) ^ _destinationType.GetHashCode();
            }
        }
    }
}
