using System;
using System.Linq;
using System.Reflection;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp.Configuration;

/// <summary>
/// Options for a member of a map defined with <c>DefineMap(Type, Type)</c>.
/// </summary>
public interface IMemberOptions
{
    /// <summary>
    /// Skips the member.
    /// </summary>
    IMemberOptions Skip();

    /// <summary>
    /// Reads the member from the source member with the given name.
    /// </summary>
    IMemberOptions MapSource(string sourceMemberName);

    /// <summary>
    /// Assigns a constant value.
    /// </summary>
    IMemberOptions UseValue(object? value);
}

/// <summary>
/// Fluent builder for a map defined with types known at run time, including open generic types.
/// </summary>
public interface IMappingDefinition
{
    /// <summary>
    /// Configures a destination member by name.
    /// </summary>
    IMappingDefinition ForProperty(string destinationMember, Action<IMemberOptions> options);

    /// <summary>
    /// Creates the reverse map.
    /// </summary>
    IMappingDefinition ReverseMap();

    /// <summary>
    /// Uses a type converter. For an open generic map it may be an open generic type, closed with the map's type arguments.
    /// </summary>
    IMappingDefinition ConvertUsing(Type converterType);

    /// <summary>
    /// Includes a derived pair for polymorphic dispatch.
    /// </summary>
    IMappingDefinition Include(Type derivedSource, Type derivedDestination);

    /// <summary>
    /// Inherits the configuration of a base pair.
    /// </summary>
    IMappingDefinition IncludeBase(Type baseSource, Type baseDestination);

    /// <summary>
    /// Sets the maximum recursion depth.
    /// </summary>
    IMappingDefinition MaxDepth(int depth);

    /// <summary>
    /// Enables reference preservation.
    /// </summary>
    IMappingDefinition PreserveReferences(bool preserve = true);

    /// <summary>
    /// Chooses which members configuration validation checks.
    /// </summary>
    IMappingDefinition ValidateMembers(MemberList memberList);
}

internal sealed class MemberOptions : IMemberOptions
{
    private readonly PropertyRule _rule;

    public MemberOptions(PropertyRule rule)
    {
        _rule = rule;
    }

    public IMemberOptions Skip()
    {
        _rule.IsIgnored = true;
        return this;
    }

    public IMemberOptions MapSource(string sourceMemberName)
    {
        if (string.IsNullOrEmpty(sourceMemberName)) throw new ArgumentException("A source member name is required.", nameof(sourceMemberName));
        _rule.SourceMemberName = sourceMemberName;
        return this;
    }

    public IMemberOptions UseValue(object? value)
    {
        _rule.ConstantValue = value;
        _rule.HasConstantValue = true;
        return this;
    }
}

internal sealed class NonGenericMappingDefinition : IMappingDefinition
{
    private readonly TypeMappingDescriptor _descriptor;
    private readonly MappingModule _module;

    public NonGenericMappingDefinition(TypeMappingDescriptor descriptor, MappingModule module)
    {
        _descriptor = descriptor;
        _module = module;
    }

    public IMappingDefinition ForProperty(string destinationMember, Action<IMemberOptions> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var member = (MemberInfo?)_descriptor.DestinationType.GetProperty(destinationMember, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                     ?? _descriptor.DestinationType.GetField(destinationMember, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                     ?? throw new AutoMapSharpConfigurationException(
                         $"Type '{_descriptor.DestinationType.FullName}' has no public property or field named '{destinationMember}'.");

        if (!_descriptor.Rules.TryGetValue(member.Name, out var rule))
        {
            rule = new PropertyRule(member);
            _descriptor.Rules[member.Name] = rule;
        }

        options(new MemberOptions(rule));
        return this;
    }

    public IMappingDefinition ReverseMap() => _module.CreateReverseMap(_descriptor);

    public IMappingDefinition ConvertUsing(Type converterType)
    {
        _descriptor.CustomConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
        return this;
    }

    public IMappingDefinition Include(Type derivedSource, Type derivedDestination)
    {
        if (!_descriptor.SourceType.IsAssignableFrom(derivedSource) || !_descriptor.DestinationType.IsAssignableFrom(derivedDestination))
        {
            throw new AutoMapSharpConfigurationException(
                $"Include({derivedSource.Name}, {derivedDestination.Name}) must name types derived from {_descriptor.SourceType.Name} and {_descriptor.DestinationType.Name}.");
        }

        _descriptor.DerivedTypes.Add((derivedSource, derivedDestination));
        return this;
    }

    public IMappingDefinition IncludeBase(Type baseSource, Type baseDestination)
    {
        var key = (baseSource, baseDestination);
        if (!_descriptor.BaseTypes.Contains(key)) _descriptor.BaseTypes.Add(key);
        return this;
    }

    public IMappingDefinition MaxDepth(int depth)
    {
        if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be greater than or equal to 1.");
        _descriptor.MaxDepth = depth;
        return this;
    }

    public IMappingDefinition PreserveReferences(bool preserve = true)
    {
        _descriptor.PreserveReferences = preserve;
        return this;
    }

    public IMappingDefinition ValidateMembers(MemberList memberList)
    {
        _descriptor.MemberListToValidate = memberList;
        return this;
    }
}
