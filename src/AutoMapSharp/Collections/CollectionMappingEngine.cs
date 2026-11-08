using System;
using AutoMapSharp.Execution;

namespace AutoMapSharp.Collections;

/// <summary>
/// Collection type checks shared with LINQ projections. Collection mapping itself is compiled by the execution plan.
/// </summary>
internal static class CollectionMappingEngine
{
    public static bool IsCollectionType(Type type) => CollectionPlanner.IsCollection(type);
}
