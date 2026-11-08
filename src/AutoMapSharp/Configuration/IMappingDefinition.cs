using System;
using System.Linq.Expressions;

namespace AutoMapSharp.Configuration;

/// <summary>
/// Fluent configuration options for a destination property.
/// </summary>
public interface IPropertyMappingOptions<TSource, TDestination, TMember>
{
    /// <summary>
    /// Skips mapping this property.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> Skip();

    /// <summary>
    /// Overrides the source value with a custom expression.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> MapSource<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Assigns a constant value to the destination property.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> UseValue(TMember value);

    /// <summary>
    /// Only maps the property when the specified condition evaluates to true.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, bool> condition);

    /// <summary>
    /// Only maps the property when the specified source condition evaluates to true.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, bool> condition);

    /// <summary>
    /// Supplies a fallback value when the source property resolves to null.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> Fallback(TMember fallbackValue);

    /// <summary>
    /// Maps this destination property using a custom resolver of type <typeparamref name="TResolver"/>.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> MapWith<TResolver>()
        where TResolver : IPropertyResolver<TSource, TDestination, TMember>;

    /// <summary>
    /// Maps this destination property using a custom resolver of the specified type.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> MapWith(Type resolverType);

    /// <summary>
    /// Prevents eager projection of this member during ProjectOnto queries unless explicitly specified in membersToExpand.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> ExplicitExpansion();

    /// <summary>
    /// Only maps the member when the condition is true. It runs before the source value is read or resolved,
    /// so a false condition also skips the resolver. Same as <see cref="If(Func{TSource, bool})"/>.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> PreCondition(Func<TSource, bool> condition);

    /// <summary>
    /// Only maps the member when the condition is true. It runs before the source value is read or resolved.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> PreCondition(Func<TSource, TDestination, bool> condition);

    /// <summary>
    /// Only assigns the member when the condition is true. It runs after the value is resolved and converted,
    /// and receives that value.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, TMember, bool> condition);

    /// <summary>
    /// Only assigns the member when the condition is true. It receives the new value and the destination's current value.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, TMember, TMember, bool> condition);

    /// <summary>
    /// Maps into the destination's existing member object or collection instead of replacing it, and keeps it when the source value is null.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> UseDestinationValue();

    /// <summary>
    /// Always creates a new member object or collection, even when the destination already holds one.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> DoNotUseDestinationValue();

    /// <summary>
    /// Converts the source member with the same name using <typeparamref name="TValueConverter"/>.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> ConvertUsing<TValueConverter>();

    /// <summary>
    /// Converts the given source member using <typeparamref name="TValueConverter"/>.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> ConvertUsing<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TValueConverter : IValueConverter<TSourceMember, TMember>;

    /// <summary>
    /// Transforms the final value of this member. Runs after module-level value transformers.
    /// </summary>
    IPropertyMappingOptions<TSource, TDestination, TMember> AddTransform(Func<TMember, TMember> transformer);
}

/// <summary>
/// Options for one constructor parameter, configured with <c>ForCtorParam</c>.
/// </summary>
public interface ICtorParamOptions<TSource>
{
    /// <summary>
    /// Supplies the parameter from a source expression.
    /// </summary>
    ICtorParamOptions<TSource> MapSource<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Supplies a constant value for the parameter.
    /// </summary>
    ICtorParamOptions<TSource> UseValue(object? value);
}

/// <summary>
/// Options for one source member, configured with <c>ForSourceMember</c>.
/// </summary>
public interface ISourceMemberOptions
{
    /// <summary>
    /// Excludes this source member from <see cref="MemberList.Source"/> validation.
    /// </summary>
    void DoNotValidate();
}

/// <summary>
/// Fluent configuration options for a nested destination member configured with <c>ForPath</c>.
/// </summary>
public interface IPathMappingOptions<TSource, TDestination, TMember>
{
    /// <summary>
    /// Skips mapping this path.
    /// </summary>
    IPathMappingOptions<TSource, TDestination, TMember> Skip();

    /// <summary>
    /// Sets the nested member from a source expression.
    /// </summary>
    IPathMappingOptions<TSource, TDestination, TMember> MapSource<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Assigns a constant value to the nested member.
    /// </summary>
    IPathMappingOptions<TSource, TDestination, TMember> UseValue(TMember value);

    /// <summary>
    /// Only maps the path when the condition evaluates to true.
    /// </summary>
    IPathMappingOptions<TSource, TDestination, TMember> If(Func<TSource, bool> condition);

    /// <summary>
    /// Only maps the path when the condition evaluates to true.
    /// </summary>
    IPathMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, bool> condition);

    /// <summary>
    /// Supplies a value to use when the source value is null.
    /// </summary>
    IPathMappingOptions<TSource, TDestination, TMember> Fallback(TMember fallbackValue);
}

/// <summary>
/// Fluent builder for configuring a type-to-type map.
/// </summary>
public interface IMappingDefinition<TSource, TDestination>
{
    /// <summary>
    /// Configures mapping rules for a specific destination property.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ForProperty<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IPropertyMappingOptions<TSource, TDestination, TMember>> memberOptions);

    /// <summary>
    /// Configures a nested destination member, for example <c>ForPath(d => d.Customer.Name, o => o.MapSource(s => s.CustomerName))</c>.
    /// Missing intermediate objects are created with their parameterless constructor.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ForPath<TMember>(
        Expression<Func<TDestination, TMember>> destinationPath,
        Action<IPathMappingOptions<TSource, TDestination, TMember>> pathOptions);

    /// <summary>
    /// Automatically generates the reverse mapping pair from destination to source.
    /// </summary>
    IMappingDefinition<TDestination, TSource> ReverseMap();

    /// <summary>
    /// Completely takes over mapping using a custom type converter of type <typeparamref name="TConverter"/>.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ConvertUsing<TConverter>()
        where TConverter : ITypeConverter<TSource, TDestination>;

    /// <summary>
    /// Completely takes over mapping using a custom type converter of the specified type.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ConvertUsing(Type converterType);

    /// <summary>
    /// Completely takes over mapping using a custom converter function with resolution context.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ConvertUsing(Func<TSource, ResolutionContext, TDestination> converter);

    /// <summary>
    /// Completely takes over mapping using a custom converter function.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ConvertUsing(Func<TSource, TDestination> converter);

    /// <summary>
    /// Includes a derived source-destination type pair for polymorphic dispatch mapping.
    /// </summary>
    IMappingDefinition<TSource, TDestination> Include<TDerivedSource, TDerivedDestination>()
        where TDerivedSource : TSource
        where TDerivedDestination : TDestination;

    /// <summary>
    /// Includes base mapping rules from the specified base source-destination type pair.
    /// </summary>
    IMappingDefinition<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>()
        where TBaseSource : class
        where TBaseDestination : class;

    /// <summary>
    /// Specifies an action to run before property mapping begins.
    /// </summary>
    IMappingDefinition<TSource, TDestination> BeforeMap(Action<TSource, TDestination, ResolutionContext> callback);

    /// <summary>
    /// Specifies an action to run before property mapping begins.
    /// </summary>
    IMappingDefinition<TSource, TDestination> BeforeMap(Action<TSource, TDestination> callback);

    /// <summary>
    /// Specifies an action to run after property mapping completes.
    /// </summary>
    IMappingDefinition<TSource, TDestination> AfterMap(Action<TSource, TDestination, ResolutionContext> callback);

    /// <summary>
    /// Specifies an action to run after property mapping completes.
    /// </summary>
    IMappingDefinition<TSource, TDestination> AfterMap(Action<TSource, TDestination> callback);

    /// <summary>
    /// Sets the maximum recursive depth for this mapping to prevent infinite cycles.
    /// </summary>
    IMappingDefinition<TSource, TDestination> MaxDepth(int depth);

    /// <summary>
    /// Enables object reference preservation to handle circular references.
    /// </summary>
    IMappingDefinition<TSource, TDestination> PreserveReferences(bool preserve = true);

    /// <summary>
    /// Custom factory function to instantiate the destination instance from the source.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> constructor);

    /// <summary>
    /// Configures whether null source collections produce a null destination collection or an empty collection.
    /// </summary>
    IMappingDefinition<TSource, TDestination> AllowNullCollections(bool allow = true);

    /// <summary>
    /// Custom factory that creates the destination from the source and the resolution context.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ConstructUsing(Func<TSource, ResolutionContext, TDestination> constructor);

    /// <summary>
    /// Configures one constructor parameter by name.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ForCtorParam(string parameterName, Action<ICtorParamOptions<TSource>> options);

    /// <summary>
    /// Runs <typeparamref name="TAction"/> before members are mapped. The action is created from the service provider when registered there.
    /// </summary>
    IMappingDefinition<TSource, TDestination> BeforeMap<TAction>() where TAction : IMappingAction<TSource, TDestination>;

    /// <summary>
    /// Runs <typeparamref name="TAction"/> after members are mapped. The action is created from the service provider when registered there.
    /// </summary>
    IMappingDefinition<TSource, TDestination> AfterMap<TAction>() where TAction : IMappingAction<TSource, TDestination>;

    /// <summary>
    /// Dispatches to the defined map of any derived source and destination pair, and gives those maps this map's
    /// configuration, without listing each pair with Include and IncludeBase.
    /// </summary>
    IMappingDefinition<TSource, TDestination> IncludeAllDerived();

    /// <summary>
    /// Maps to <typeparamref name="TOther"/> instead, using the map from <typeparamref name="TSource"/> to <typeparamref name="TOther"/>.
    /// </summary>
    IMappingDefinition<TSource, TDestination> As<TOther>() where TOther : TDestination;

    /// <summary>
    /// Chooses which members configuration validation checks for this map.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ValidateMembers(MemberList memberList);

    /// <summary>
    /// Configures a source member, for example to exclude it from <see cref="MemberList.Source"/> validation.
    /// </summary>
    IMappingDefinition<TSource, TDestination> ForSourceMember<TMember>(Expression<Func<TSource, TMember>> sourceMember, Action<ISourceMemberOptions> options);
}
