using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AutoMapSharp;

/// <summary>
/// Defines a map from <see cref="SourceType"/> to the class this attribute is placed on.
/// Picked up by <c>AddAttributeMaps(assembly)</c> and by dependency injection assembly scanning.
/// Equivalent to AutoMapper's <c>[AutoMap]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapFromAttribute : Attribute
{
    /// <summary>Declares a map from <paramref name="sourceType"/> to the type this attribute is applied to.</summary>
    /// <param name="sourceType">The source type to map from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceType"/> is null.</exception>
    public MapFromAttribute(Type sourceType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
    }

    /// <summary>
    /// The source type of the map.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Also defines the reverse map.
    /// </summary>
    public bool ReverseMap { get; set; }
}

/// <summary>
/// Skips this destination member in every map, unless a map configures it explicitly. Equivalent to AutoMapper's <c>[Ignore]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SkipMapAttribute : Attribute
{
}

/// <summary>
/// Reads this destination member from the source member with the given name, in every map unless a map configures
/// it explicitly. Equivalent to AutoMapper's <c>[SourceMember]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SourceMemberAttribute : Attribute
{
    /// <summary>Reads this member from the source member called <paramref name="name"/>.</summary>
    /// <param name="name">The name of the source member.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public SourceMemberAttribute(string name)
    {
        Name = string.IsNullOrEmpty(name) ? throw new ArgumentException("A source member name is required.", nameof(name)) : name;
    }

    /// <summary>
    /// The name of the source member.
    /// </summary>
    public string Name { get; }
}

/// <summary>
/// A module that holds the maps defined with <see cref="MapFromAttribute"/> in a set of assemblies.
/// </summary>
public sealed class AttributeMappingModule : MappingModule
{
    /// <summary>Creates a module with a map for every <see cref="MapFromAttribute"/> found on types in <paramref name="assemblies"/>.</summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    public AttributeMappingModule(params Assembly[] assemblies)
    {
        foreach (var type in assemblies.SelectMany(GetTypes).Distinct())
        {
            foreach (var attribute in type.GetCustomAttributes<MapFromAttribute>(inherit: false))
            {
                var definition = DefineMap(attribute.SourceType, type);
                if (attribute.ReverseMap)
                {
                    definition.ReverseMap();
                }
            }
        }
    }

    /// <summary>
    /// True when any type in the assembly carries <see cref="MapFromAttribute"/>.
    /// </summary>
    public static bool HasAttributeMaps(Assembly assembly) => GetTypes(assembly).Any(t => t.IsDefined(typeof(MapFromAttribute), false));

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
