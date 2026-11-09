using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp.Execution;

/// <summary>
/// Builds collection mapping expressions. The strategy is chosen once per destination type when the plan is built.
/// </summary>
internal static class CollectionPlanner
{
    public static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && GetElementType(type) != null;

    public static bool IsEnumerable(Type type) => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>
    /// The element type of a collection, or <c>KeyValuePair&lt;TKey, TValue&gt;</c> for a dictionary.
    /// </summary>
    public static Type? GetElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        var enumerable = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable != null) return enumerable.GetGenericArguments()[0];

        return typeof(IEnumerable).IsAssignableFrom(type) ? typeof(object) : null;
    }

    public static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        var element = GetElementType(type);
        if (element != null && element.IsGenericType && element.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) &&
            (IsGenericInterface(type, typeof(IDictionary<,>)) || IsGenericInterface(type, typeof(IReadOnlyDictionary<,>))))
        {
            var args = element.GetGenericArguments();
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        keyType = valueType = typeof(object);
        return false;
    }

    private static bool IsGenericInterface(Type type, Type definition) =>
        (type.IsGenericType && type.GetGenericTypeDefinition() == definition) ||
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);

    /// <summary>
    /// Builds an expression that maps the enumerable <paramref name="source"/> into a collection of type <paramref name="destinationType"/>.
    /// </summary>
    /// <param name="source">The source collection. It may be null at run time.</param>
    /// <param name="destinationType">The destination collection type.</param>
    /// <param name="existing">The destination's current collection, refilled in place when it is mutable, or null.</param>
    /// <param name="context">The resolution context expression.</param>
    /// <param name="allowNull">When true a null source gives null, otherwise an empty collection.</param>
    /// <param name="elementConverter">Builds the conversion of one source element to one destination element.</param>
    /// <param name="mergeLookup">Finds the equality comparison for an element pair, used to merge into an existing collection instead of refilling it.</param>
    public static Expression Build(
        Expression source,
        Type destinationType,
        Expression? existing,
        Expression context,
        bool allowNull,
        Func<Type, Type, LambdaExpression> elementConverter,
        Func<Type, Type, (Delegate Comparison, Delegate MapOnto)?>? mergeLookup = null)
    {
        if (TryGetDictionaryTypes(destinationType, out var destinationKey, out var destinationValue) &&
            TryGetDictionaryTypes(source.Type, out var sourceKey, out var sourceValue) ||
            TryGetDictionaryTypes(destinationType, out destinationKey, out destinationValue) &&
            TryGetKeyValueElement(source.Type, out sourceKey, out sourceValue))
        {
            return BuildDictionary(source, destinationType, existing, context, allowNull,
                sourceKey, sourceValue, destinationKey, destinationValue, elementConverter);
        }

        var sourceElement = GetElementType(source.Type)!;
        var destinationElement = GetElementType(destinationType)!;

        Expression typedSource = source;
        if (!typeof(IEnumerable<>).MakeGenericType(sourceElement).IsAssignableFrom(source.Type))
        {
            // A non-generic IEnumerable source: read its elements as object.
            typedSource = Expression.Call(typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!.MakeGenericMethod(typeof(object)), Expression.Convert(source, typeof(IEnumerable)));
            sourceElement = typeof(object);
        }

        var converter = elementConverter(sourceElement, destinationElement);
        var converterConstant = Expression.Constant(PlanCompiler.Compile(converter), converter.Type);
        var create = BuildCreate(destinationType, sourceElement, destinationElement, converterConstant, context);

        var helper = typeof(CollectionRuntime);
        var sourceVariable = Expression.Variable(typeof(IEnumerable<>).MakeGenericType(sourceElement), "items");
        var emptySource = Expression.Call(typeof(Enumerable).GetMethod(nameof(Enumerable.Empty))!.MakeGenericMethod(sourceElement));

        // Refill a mutable existing collection in place, which keeps its identity, for example an EF Core navigation collection.
        Expression whenSource = create(sourceVariable);
        Expression whenNull = allowNull ? Expression.Constant(null, destinationType) : create(emptySource);

        var canFillExisting = existing != null && !destinationType.IsArray &&
            (destinationType.IsInterface || typeof(ICollection<>).MakeGenericType(destinationElement).IsAssignableFrom(destinationType));
        var merge = canFillExisting ? mergeLookup?.Invoke(sourceElement, destinationElement) : null;
        if (merge is { } m)
        {
            // AutoMapSharp.Collections: match items with EqualityComparison, update matches in place, add and remove the rest.
            var tryMerge = helper.GetMethod(nameof(CollectionRuntime.TryMergeExisting))!.MakeGenericMethod(sourceElement, destinationElement);
            Expression Merge(Expression items) => Expression.Call(tryMerge, Expression.Convert(existing!, typeof(object)), items,
                Expression.Constant(m.Comparison), converterConstant, Expression.Constant(m.MapOnto), context);
            whenSource = Expression.Condition(Merge(sourceVariable), existing!, whenSource);
            if (!allowNull)
            {
                whenNull = Expression.Condition(Merge(emptySource), existing!, whenNull);
            }
        }
        else if (canFillExisting)
        {
            var tryFill = helper.GetMethod(nameof(CollectionRuntime.TryFillExisting))!.MakeGenericMethod(sourceElement, destinationElement);
            whenSource = Expression.Condition(
                Expression.Call(tryFill, Expression.Convert(existing!, typeof(object)), sourceVariable, converterConstant, context),
                existing!,
                whenSource);
            if (!allowNull)
            {
                whenNull = Expression.Condition(
                    Expression.Call(tryFill, Expression.Convert(existing!, typeof(object)), emptySource, converterConstant, context),
                    existing!,
                    whenNull);
            }
        }

        return Expression.Block(
            destinationType,
            new[] { sourceVariable },
            Expression.Assign(sourceVariable, Expression.Convert(typedSource, sourceVariable.Type)),
            Expression.Condition(Expression.Equal(sourceVariable, Expression.Constant(null, sourceVariable.Type)), whenNull, whenSource));
    }

    private static bool TryGetKeyValueElement(Type type, out Type keyType, out Type valueType)
    {
        var element = GetElementType(type);
        if (element != null && element.IsGenericType && element.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var args = element.GetGenericArguments();
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        keyType = valueType = typeof(object);
        return false;
    }

    /// <summary>
    /// Returns a function that, given an <c>IEnumerable&lt;TSource&gt;</c> expression, creates the destination collection.
    /// </summary>
    private static Func<Expression, Expression> BuildCreate(Type destinationType, Type sourceElement, Type destinationElement, ConstantExpression converterConstant, Expression context)
    {
        var helper = typeof(CollectionRuntime);
        var listType = typeof(List<>).MakeGenericType(destinationElement);

        Expression ToList(Expression items) =>
            Expression.Call(helper.GetMethod(nameof(CollectionRuntime.ToList))!.MakeGenericMethod(sourceElement, destinationElement), items, converterConstant, context);

        // Arrays.
        if (destinationType.IsArray)
        {
            return items => Expression.Call(helper.GetMethod(nameof(CollectionRuntime.ToArray))!.MakeGenericMethod(sourceElement, destinationElement), items, converterConstant, context);
        }

        // Immutable collections: ImmutableList<T> -> ImmutableList.CreateRange(items) and similar.
        var createRange = FindCreateRange(destinationType, destinationElement);
        if (createRange != null)
        {
            return items => Expression.Call(createRange, ToList(items));
        }

        // Interfaces: IEnumerable<T>, IList<T>, IReadOnlyList<T>, ISet<T>, IReadOnlySet<T> and so on.
        if (destinationType.IsInterface || destinationType.IsAbstract)
        {
            if (destinationType.IsAssignableFrom(listType))
            {
                return items => Expression.Convert(ToList(items), destinationType);
            }

            var setType = typeof(HashSet<>).MakeGenericType(destinationElement);
            if (destinationType.IsAssignableFrom(setType))
            {
                var fillNew = helper.GetMethod(nameof(CollectionRuntime.FillNew))!.MakeGenericMethod(setType, sourceElement, destinationElement);
                return items => Expression.Convert(Expression.Call(fillNew, items, converterConstant, context), destinationType);
            }

            return Unsupported(destinationType);
        }

        // Concrete types with a parameterless constructor and an Add method: List<T>, HashSet<T>, SortedSet<T>,
        // ObservableCollection<T>, Collection<T>, LinkedList<T> and so on.
        if (!destinationType.IsValueType &&
            destinationType.GetConstructor(Type.EmptyTypes) != null &&
            typeof(ICollection<>).MakeGenericType(destinationElement).IsAssignableFrom(destinationType))
        {
            if (destinationType == listType)
            {
                return ToList;
            }

            var fillNew = helper.GetMethod(nameof(CollectionRuntime.FillNew))!.MakeGenericMethod(destinationType, sourceElement, destinationElement);
            return items => Expression.Call(fillNew, items, converterConstant, context);
        }

        // Types built from a list: Queue<T>, Stack<T>, ReadOnlyCollection<T>, ReadOnlyObservableCollection<T> and so on.
        foreach (var ctor in destinationType.GetConstructors().Where(c => c.GetParameters().Length == 1))
        {
            var parameterType = ctor.GetParameters()[0].ParameterType;
            if (parameterType.IsAssignableFrom(listType))
            {
                return items => Expression.New(ctor, Expression.Convert(ToList(items), parameterType));
            }

            var observable = typeof(System.Collections.ObjectModel.ObservableCollection<>).MakeGenericType(destinationElement);
            if (parameterType == observable)
            {
                var fillNew = helper.GetMethod(nameof(CollectionRuntime.FillNew))!.MakeGenericMethod(observable, sourceElement, destinationElement);
                return items => Expression.New(ctor, Expression.Call(fillNew, items, converterConstant, context));
            }
        }

        return Unsupported(destinationType);
    }

    private static Expression BuildDictionary(
        Expression source,
        Type destinationType,
        Expression? existing,
        Expression context,
        bool allowNull,
        Type sourceKey,
        Type sourceValue,
        Type destinationKey,
        Type destinationValue,
        Func<Type, Type, LambdaExpression> elementConverter)
    {
        var helper = typeof(CollectionRuntime);
        var pairType = typeof(KeyValuePair<,>).MakeGenericType(sourceKey, sourceValue);
        var itemsType = typeof(IEnumerable<>).MakeGenericType(pairType);
        var keyConverter = elementConverter(sourceKey, destinationKey);
        var valueConverter = elementConverter(sourceValue, destinationValue);
        var keyConstant = Expression.Constant(PlanCompiler.Compile(keyConverter), keyConverter.Type);
        var valueConstant = Expression.Constant(PlanCompiler.Compile(valueConverter), valueConverter.Type);
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(destinationKey, destinationValue);
        var typeArgs = new[] { sourceKey, sourceValue, destinationKey, destinationValue };

        Expression ToDictionary(Expression items) =>
            Expression.Call(helper.GetMethod(nameof(CollectionRuntime.ToDictionary))!.MakeGenericMethod(typeArgs), items, keyConstant, valueConstant, context);

        Func<Expression, Expression> create;
        var createRange = FindCreateRange(destinationType, typeof(KeyValuePair<,>).MakeGenericType(destinationKey, destinationValue), destinationKey, destinationValue);
        if (destinationType == dictionaryType)
        {
            create = ToDictionary;
        }
        else if (createRange != null)
        {
            create = items => Expression.Call(createRange, ToDictionary(items));
        }
        else if (destinationType.IsInterface && destinationType.IsAssignableFrom(dictionaryType))
        {
            create = items => Expression.Convert(ToDictionary(items), destinationType);
        }
        else if (!destinationType.IsValueType && destinationType.GetConstructor(Type.EmptyTypes) != null &&
                 typeof(IDictionary<,>).MakeGenericType(destinationKey, destinationValue).IsAssignableFrom(destinationType))
        {
            var fillNew = helper.GetMethod(nameof(CollectionRuntime.FillNewDictionary))!.MakeGenericMethod(new[] { destinationType }.Concat(typeArgs).ToArray());
            create = items => Expression.Call(fillNew, items, keyConstant, valueConstant, context);
        }
        else
        {
            create = Unsupported(destinationType);
        }

        var items = Expression.Variable(itemsType, "pairs");
        var empty = Expression.Call(typeof(Enumerable).GetMethod(nameof(Enumerable.Empty))!.MakeGenericMethod(pairType));
        Expression whenSource = create(items);
        Expression whenNull = allowNull ? Expression.Constant(null, destinationType) : create(empty);

        if (existing != null && (destinationType.IsInterface || typeof(IDictionary<,>).MakeGenericType(destinationKey, destinationValue).IsAssignableFrom(destinationType)))
        {
            var tryFill = helper.GetMethod(nameof(CollectionRuntime.TryFillExistingDictionary))!.MakeGenericMethod(typeArgs);
            whenSource = Expression.Condition(Expression.Call(tryFill, Expression.Convert(existing, typeof(object)), items, keyConstant, valueConstant, context), existing, whenSource);
            if (!allowNull)
            {
                whenNull = Expression.Condition(Expression.Call(tryFill, Expression.Convert(existing, typeof(object)), empty, keyConstant, valueConstant, context), existing, whenNull);
            }
        }

        return Expression.Block(
            destinationType,
            new[] { items },
            Expression.Assign(items, Expression.Convert(source, itemsType)),
            Expression.Condition(Expression.Equal(items, Expression.Constant(null, itemsType)), whenNull, whenSource));
    }

    /// <summary>
    /// Finds <c>ImmutableX.CreateRange&lt;T&gt;(IEnumerable&lt;T&gt;)</c> for an immutable collection type.
    /// </summary>
    private static MethodInfo? FindCreateRange(Type destinationType, Type elementType, params Type[] genericArguments)
    {
        if (destinationType.Namespace != "System.Collections.Immutable")
        {
            return null;
        }

        var name = destinationType.IsGenericType ? destinationType.Name.Substring(0, destinationType.Name.IndexOf('`')) : destinationType.Name;
        if (destinationType.IsInterface && name.StartsWith("I", StringComparison.Ordinal))
        {
            name = name.Substring(1);
        }

        var factory = destinationType.Assembly.GetType("System.Collections.Immutable." + name);
        var args = genericArguments.Length > 0 ? genericArguments : new[] { elementType };
        var method = factory?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CreateRange" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == args.Length && m.GetParameters().Length == 1);
        if (method == null)
        {
            return null;
        }

        var closed = method.MakeGenericMethod(args);
        return destinationType.IsAssignableFrom(closed.ReturnType) ? closed : null;
    }

    private static Func<Expression, Expression> Unsupported(Type destinationType)
    {
        var message = $"Destination collection type '{destinationType.FullName}' is not supported. " +
                      "Use a type with a parameterless constructor and an Add method, a type with a constructor that takes a list, an array, or a collection interface.";
        return _ => Expression.Throw(Expression.New(typeof(AutoMapSharpMappingException).GetConstructor(new[] { typeof(string), typeof(Type), typeof(Type), typeof(Exception) })!,
            Expression.Constant(message), Expression.Constant(null, typeof(Type)), Expression.Constant(destinationType, typeof(Type)), Expression.Constant(null, typeof(Exception))), destinationType);
    }
}

/// <summary>
/// Collection loops used by compiled plans.
/// </summary>
internal static class CollectionRuntime
{
    public static List<TOut> ToList<TIn, TOut>(IEnumerable<TIn> items, Func<TIn, ResolutionContext, TOut> convert, ResolutionContext context)
    {
        var list = items is ICollection<TIn> sized ? new List<TOut>(sized.Count) : items is IReadOnlyCollection<TIn> readOnly ? new List<TOut>(readOnly.Count) : new List<TOut>();
        foreach (var item in items)
        {
            list.Add(convert(item, context));
        }

        return list;
    }

    public static TOut[] ToArray<TIn, TOut>(IEnumerable<TIn> items, Func<TIn, ResolutionContext, TOut> convert, ResolutionContext context)
    {
        if (items is IReadOnlyList<TIn> list)
        {
            var array = new TOut[list.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = convert(list[i], context);
            }

            return array;
        }

        return ToList(items, convert, context).ToArray();
    }

    public static TCollection FillNew<TCollection, TIn, TOut>(IEnumerable<TIn> items, Func<TIn, ResolutionContext, TOut> convert, ResolutionContext context)
        where TCollection : ICollection<TOut>, new()
    {
        var collection = new TCollection();
        foreach (var item in items)
        {
            collection.Add(convert(item, context));
        }

        return collection;
    }

    /// <summary>
    /// Clears and refills <paramref name="existing"/> when it is a mutable collection. Returns false otherwise.
    /// </summary>
    public static bool TryFillExisting<TIn, TOut>(object? existing, IEnumerable<TIn> items, Func<TIn, ResolutionContext, TOut> convert, ResolutionContext context)
    {
        if (existing is not ICollection<TOut> collection || collection.IsReadOnly || existing is Array)
        {
            return false;
        }

        // Convert first, so the source can safely be the same collection as the destination.
        var converted = ToList(items, convert, context);
        collection.Clear();
        foreach (var item in converted)
        {
            collection.Add(item);
        }

        return true;
    }

    /// <summary>
    /// Merges <paramref name="items"/> into <paramref name="existing"/>: a destination item that matches a source item
    /// is updated in place, a source item without a match is added, and a destination item without a match is removed.
    /// </summary>
    public static bool TryMergeExisting<TIn, TOut>(
        object? existing,
        IEnumerable<TIn> items,
        Func<TIn, TOut, bool> equals,
        Func<TIn, ResolutionContext, TOut> mapNew,
        Func<TIn, TOut, ResolutionContext, TOut> mapOnto,
        ResolutionContext context)
    {
        if (existing is not ICollection<TOut> collection || collection.IsReadOnly || existing is Array)
        {
            return false;
        }

        var current = collection.ToList();
        var matched = new HashSet<object>(Context.AutoMapSharpReferenceEqualityComparer.Instance);
        var added = new List<TOut>();

        foreach (var item in items)
        {
            var match = current.FirstOrDefault(d => d is not null && !matched.Contains(d) && equals(item, d));
            if (match is not null)
            {
                matched.Add(match);
                mapOnto(item, match, context);
            }
            else
            {
                added.Add(mapNew(item, context));
            }
        }

        foreach (var stale in current.Where(d => d is null || !matched.Contains(d)))
        {
            collection.Remove(stale);
        }

        foreach (var item in added)
        {
            collection.Add(item);
        }

        return true;
    }

    public static Dictionary<TKey, TValue> ToDictionary<TSourceKey, TSourceValue, TKey, TValue>(
        IEnumerable<KeyValuePair<TSourceKey, TSourceValue>> items,
        Func<TSourceKey, ResolutionContext, TKey> convertKey,
        Func<TSourceValue, ResolutionContext, TValue> convertValue,
        ResolutionContext context)
        where TKey : notnull
    {
        var dictionary = items is ICollection<KeyValuePair<TSourceKey, TSourceValue>> sized ? new Dictionary<TKey, TValue>(sized.Count) : new Dictionary<TKey, TValue>();
        foreach (var pair in items)
        {
            dictionary[convertKey(pair.Key, context)] = convertValue(pair.Value, context);
        }

        return dictionary;
    }

    public static TDictionary FillNewDictionary<TDictionary, TSourceKey, TSourceValue, TKey, TValue>(
        IEnumerable<KeyValuePair<TSourceKey, TSourceValue>> items,
        Func<TSourceKey, ResolutionContext, TKey> convertKey,
        Func<TSourceValue, ResolutionContext, TValue> convertValue,
        ResolutionContext context)
        where TDictionary : IDictionary<TKey, TValue>, new()
    {
        var dictionary = new TDictionary();
        foreach (var pair in items)
        {
            dictionary[convertKey(pair.Key, context)] = convertValue(pair.Value, context);
        }

        return dictionary;
    }

    public static bool TryFillExistingDictionary<TSourceKey, TSourceValue, TKey, TValue>(
        object? existing,
        IEnumerable<KeyValuePair<TSourceKey, TSourceValue>> items,
        Func<TSourceKey, ResolutionContext, TKey> convertKey,
        Func<TSourceValue, ResolutionContext, TValue> convertValue,
        ResolutionContext context)
    {
        if (existing is not IDictionary<TKey, TValue> dictionary || dictionary.IsReadOnly)
        {
            return false;
        }

        var converted = new List<KeyValuePair<TKey, TValue>>();
        foreach (var pair in items)
        {
            converted.Add(new KeyValuePair<TKey, TValue>(convertKey(pair.Key, context), convertValue(pair.Value, context)));
        }

        dictionary.Clear();
        foreach (var pair in converted)
        {
            dictionary[pair.Key] = pair.Value;
        }

        return true;
    }
}
