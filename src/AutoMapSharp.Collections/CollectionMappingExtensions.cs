using System;
using System.Linq.Expressions;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp.Collections;

/// <summary>
/// Collection merging for AutoMapSharp. Equivalent to AutoMapper.Collection.
/// </summary>
public static class CollectionMappingExtensions
{
    /// <summary>
    /// Matches source items to existing destination items, for example by key, when a collection is mapped onto an
    /// existing collection. Matched items are updated in place, unmatched source items are added, and destination
    /// items without a match are removed. This keeps EF Core tracked entities instead of replacing them.
    /// </summary>
    /// <example>
    /// <code>DefineMap&lt;OrderLineDto, OrderLine&gt;().EqualityComparison((dto, line) =&gt; dto.Id == line.Id);</code>
    /// </example>
    public static IMappingDefinition<TSource, TDestination> EqualityComparison<TSource, TDestination>(
        this IMappingDefinition<TSource, TDestination> definition,
        Expression<Func<TSource, TDestination, bool>> comparison)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (comparison == null) throw new ArgumentNullException(nameof(comparison));

        if (definition is not IDescriptorAccess access)
        {
            throw new AutoMapSharpConfigurationException("EqualityComparison can only be used on maps defined with DefineMap.");
        }

        access.Descriptor.EqualityComparison = comparison.Compile();
        return definition;
    }
}
