using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp.Configuration;

internal sealed class PropertyMappingOptions<TSource, TDestination, TMember> : IPropertyMappingOptions<TSource, TDestination, TMember>
{
    private readonly PropertyRule _rule;

    public PropertyMappingOptions(PropertyRule rule)
    {
        _rule = rule;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> Skip()
    {
        _rule.IsIgnored = true;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> MapSource<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        _rule.SourceExpression = sourceMember;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> UseValue(TMember value)
    {
        _rule.ConstantValue = value;
        _rule.HasConstantValue = true;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, bool> condition)
    {
        _rule.Condition = condition;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, bool> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        _rule.Condition = new Func<TSource, TDestination, bool>((src, _) => condition(src));
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> Fallback(TMember fallbackValue)
    {
        _rule.FallbackValue = fallbackValue;
        _rule.HasFallbackValue = true;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> MapWith<TResolver>()
        where TResolver : IPropertyResolver<TSource, TDestination, TMember>
    {
        return MapWith(typeof(TResolver));
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> MapWith(Type resolverType)
    {
        if (resolverType == null) throw new ArgumentNullException(nameof(resolverType));
        _rule.CustomResolverType = resolverType;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> ExplicitExpansion()
    {
        _rule.ExplicitExpansion = true;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> PreCondition(Func<TSource, bool> condition) => If(condition);

    public IPropertyMappingOptions<TSource, TDestination, TMember> PreCondition(Func<TSource, TDestination, bool> condition) => If(condition);

    public IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, TMember, bool> condition)
    {
        _rule.ValueCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, TMember, TMember, bool> condition)
    {
        _rule.ValueCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> UseDestinationValue()
    {
        _rule.UseDestinationValue = true;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> DoNotUseDestinationValue()
    {
        _rule.UseDestinationValue = false;
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> ConvertUsing<TValueConverter>()
    {
        if (!typeof(TValueConverter).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueConverter<,>)))
        {
            throw new AutoMapSharpConfigurationException($"Type '{typeof(TValueConverter).FullName}' does not implement IValueConverter<TSourceMember, TDestinationMember>.");
        }

        _rule.ValueConverterType = typeof(TValueConverter);
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> ConvertUsing<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TValueConverter : IValueConverter<TSourceMember, TMember>
    {
        _rule.SourceExpression = sourceMember ?? throw new ArgumentNullException(nameof(sourceMember));
        _rule.ValueConverterType = typeof(TValueConverter);
        return this;
    }

    public IPropertyMappingOptions<TSource, TDestination, TMember> AddTransform(Func<TMember, TMember> transformer)
    {
        _rule.MemberTransformers.Add(transformer ?? throw new ArgumentNullException(nameof(transformer)));
        return this;
    }
}

internal sealed class CtorParamOptions<TSource> : ICtorParamOptions<TSource>
{
    private readonly CtorParamRule _rule;

    public CtorParamOptions(CtorParamRule rule)
    {
        _rule = rule;
    }

    public ICtorParamOptions<TSource> MapSource<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        _rule.SourceExpression = sourceMember ?? throw new ArgumentNullException(nameof(sourceMember));
        _rule.HasConstantValue = false;
        return this;
    }

    public ICtorParamOptions<TSource> UseValue(object? value)
    {
        _rule.ConstantValue = value;
        _rule.HasConstantValue = true;
        _rule.SourceExpression = null;
        return this;
    }
}

internal sealed class SourceMemberOptions : ISourceMemberOptions
{
    private readonly TypeMappingDescriptor _descriptor;
    private readonly string _name;

    public SourceMemberOptions(TypeMappingDescriptor descriptor, string name)
    {
        _descriptor = descriptor;
        _name = name;
    }

    public void DoNotValidate() => _descriptor.UnvalidatedSourceMemberNames.Add(_name);
}

internal sealed class PathMappingOptions<TSource, TDestination, TMember> : IPathMappingOptions<TSource, TDestination, TMember>
{
    private readonly PropertyRule _rule;

    public PathMappingOptions(PropertyRule rule)
    {
        _rule = rule;
    }

    public IPathMappingOptions<TSource, TDestination, TMember> Skip()
    {
        _rule.IsIgnored = true;
        return this;
    }

    public IPathMappingOptions<TSource, TDestination, TMember> MapSource<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        _rule.SourceExpression = sourceMember ?? throw new ArgumentNullException(nameof(sourceMember));
        return this;
    }

    public IPathMappingOptions<TSource, TDestination, TMember> UseValue(TMember value)
    {
        _rule.ConstantValue = value;
        _rule.HasConstantValue = true;
        return this;
    }

    public IPathMappingOptions<TSource, TDestination, TMember> If(Func<TSource, bool> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        _rule.Condition = new Func<TSource, TDestination, bool>((src, _) => condition(src));
        return this;
    }

    public IPathMappingOptions<TSource, TDestination, TMember> If(Func<TSource, TDestination, bool> condition)
    {
        _rule.Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public IPathMappingOptions<TSource, TDestination, TMember> Fallback(TMember fallbackValue)
    {
        _rule.FallbackValue = fallbackValue;
        _rule.HasFallbackValue = true;
        return this;
    }
}

/// <summary>
/// Gives companion packages, such as AutoMapSharp.Collections, access to the map being configured.
/// </summary>
internal interface IDescriptorAccess
{
    TypeMappingDescriptor Descriptor { get; }
}

internal sealed class MappingDefinition<TSource, TDestination> : IMappingDefinition<TSource, TDestination>, IDescriptorAccess
{
    TypeMappingDescriptor IDescriptorAccess.Descriptor => _descriptor;

    private readonly TypeMappingDescriptor _descriptor;
    private readonly MappingModule _module;

    public MappingDefinition(TypeMappingDescriptor descriptor, MappingModule module)
    {
        _descriptor = descriptor;
        _module = module;
    }

    public IMappingDefinition<TSource, TDestination> ForProperty<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IPropertyMappingOptions<TSource, TDestination, TMember>> memberOptions)
    {
        if (destinationMember == null) throw new ArgumentNullException(nameof(destinationMember));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));

        var memberInfo = ExtractMemberInfo(destinationMember);
        if (!_descriptor.Rules.TryGetValue(memberInfo.Name, out var rule))
        {
            rule = new PropertyRule(memberInfo);
            _descriptor.Rules[memberInfo.Name] = rule;
        }

        var options = new PropertyMappingOptions<TSource, TDestination, TMember>(rule);
        memberOptions(options);
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ForPath<TMember>(
        Expression<Func<TDestination, TMember>> destinationPath,
        Action<IPathMappingOptions<TSource, TDestination, TMember>> pathOptions)
    {
        if (destinationPath == null) throw new ArgumentNullException(nameof(destinationPath));
        if (pathOptions == null) throw new ArgumentNullException(nameof(pathOptions));

        var chain = MappingModule.TryGetPropertyChain(destinationPath);
        if (chain == null || chain.Count == 0)
        {
            throw new AutoMapSharpConfigurationException(
                $"ForPath expression '{destinationPath}' must be a chain of properties starting at the destination, such as d => d.Customer.Name.");
        }

        if (chain.Count == 1)
        {
            throw new AutoMapSharpConfigurationException(
                $"ForPath expression '{destinationPath}' refers to a top-level member. Use ForProperty for top-level members.");
        }

        var key = string.Join(".", System.Linq.Enumerable.Select(chain, p => p.Name));
        var existing = _descriptor.Paths.Find(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        var rule = existing?.Rule ?? new PropertyRule(chain[chain.Count - 1]);
        if (existing == null)
        {
            _descriptor.Paths.Add(new PathRule(chain, rule));
        }

        pathOptions(new PathMappingOptions<TSource, TDestination, TMember>(rule));
        return this;
    }

    public IMappingDefinition<TDestination, TSource> ReverseMap()
    {
        return _module.CreateReverseMap<TSource, TDestination>(_descriptor);
    }

    public IMappingDefinition<TSource, TDestination> ConvertUsing<TConverter>()
        where TConverter : ITypeConverter<TSource, TDestination>
    {
        return ConvertUsing(typeof(TConverter));
    }

    public IMappingDefinition<TSource, TDestination> ConvertUsing(Type converterType)
    {
        if (converterType == null) throw new ArgumentNullException(nameof(converterType));
        _descriptor.CustomConverterType = converterType;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ConvertUsing(Func<TSource, ResolutionContext, TDestination> converter)
    {
        if (converter == null) throw new ArgumentNullException(nameof(converter));
        _descriptor.CustomConverter = converter;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ConvertUsing(Func<TSource, TDestination> converter)
    {
        if (converter == null) throw new ArgumentNullException(nameof(converter));
        _descriptor.CustomConverter = new Func<TSource, ResolutionContext, TDestination>((src, _) => converter(src));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> Include<TDerivedSource, TDerivedDestination>()
        where TDerivedSource : TSource
        where TDerivedDestination : TDestination
    {
        _descriptor.DerivedTypes.Add((typeof(TDerivedSource), typeof(TDerivedDestination)));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>()
        where TBaseSource : class
        where TBaseDestination : class
    {
        var baseType = (typeof(TBaseSource), typeof(TBaseDestination));
        if (!_descriptor.BaseTypes.Contains(baseType))
        {
            _descriptor.BaseTypes.Add(baseType);
        }

        return this;
    }

    public IMappingDefinition<TSource, TDestination> BeforeMap(Action<TSource, TDestination, ResolutionContext> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        _descriptor.BeforeActions.Add(callback);
        return this;
    }

    public IMappingDefinition<TSource, TDestination> BeforeMap(Action<TSource, TDestination> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        _descriptor.BeforeActions.Add(new Action<TSource, TDestination, ResolutionContext>((s, d, _) => callback(s, d)));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> AfterMap(Action<TSource, TDestination, ResolutionContext> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        _descriptor.AfterActions.Add(callback);
        return this;
    }

    public IMappingDefinition<TSource, TDestination> AfterMap(Action<TSource, TDestination> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        _descriptor.AfterActions.Add(new Action<TSource, TDestination, ResolutionContext>((s, d, _) => callback(s, d)));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> MaxDepth(int depth)
    {
        if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be greater than or equal to 1.");
        _descriptor.MaxDepth = depth;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> PreserveReferences(bool preserve = true)
    {
        _descriptor.PreserveReferences = preserve;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> constructor)
    {
        if (constructor == null) throw new ArgumentNullException(nameof(constructor));
        _descriptor.ConstructorExpression = constructor;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> AllowNullCollections(bool allow = true)
    {
        _descriptor.AllowNullCollections = allow;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ConstructUsing(Func<TSource, ResolutionContext, TDestination> constructor)
    {
        _descriptor.ConstructorExpression = constructor ?? throw new ArgumentNullException(nameof(constructor));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ForCtorParam(string parameterName, Action<ICtorParamOptions<TSource>> options)
    {
        if (string.IsNullOrEmpty(parameterName)) throw new ArgumentException("A parameter name is required.", nameof(parameterName));
        if (options == null) throw new ArgumentNullException(nameof(options));

        if (!_descriptor.CtorRules.TryGetValue(parameterName, out var rule))
        {
            rule = new CtorParamRule(parameterName);
            _descriptor.CtorRules[parameterName] = rule;
        }

        options(new CtorParamOptions<TSource>(rule));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> BeforeMap<TAction>() where TAction : IMappingAction<TSource, TDestination>
    {
        var factory = new Execution.InstanceFactory(typeof(TAction));
        _descriptor.BeforeActions.Add(new Action<TSource, TDestination, ResolutionContext>((s, d, c) => ((TAction)factory.Create(c)).Process(s, d, c)));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> AfterMap<TAction>() where TAction : IMappingAction<TSource, TDestination>
    {
        var factory = new Execution.InstanceFactory(typeof(TAction));
        _descriptor.AfterActions.Add(new Action<TSource, TDestination, ResolutionContext>((s, d, c) => ((TAction)factory.Create(c)).Process(s, d, c)));
        return this;
    }

    public IMappingDefinition<TSource, TDestination> IncludeAllDerived()
    {
        _descriptor.IncludeAllDerivedTypes = true;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> As<TOther>() where TOther : TDestination
    {
        _descriptor.AsType = typeof(TOther);
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ValidateMembers(MemberList memberList)
    {
        _descriptor.MemberListToValidate = memberList;
        return this;
    }

    public IMappingDefinition<TSource, TDestination> ForSourceMember<TMember>(Expression<Func<TSource, TMember>> sourceMember, Action<ISourceMemberOptions> options)
    {
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var body = sourceMember.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary ? unary.Operand : sourceMember.Body;
        if (body is not MemberExpression { Expression: ParameterExpression } member)
        {
            throw new AutoMapSharpConfigurationException($"ForSourceMember expression '{sourceMember}' must refer to a top-level source member.");
        }

        options(new SourceMemberOptions(_descriptor, member.Member.Name));
        return this;
    }

    private static MemberInfo ExtractMemberInfo<TMember>(Expression<Func<TDestination, TMember>> expression)
    {
        Expression body = expression.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression memberExpr)
        {
            if (memberExpr.Expression is not ParameterExpression)
            {
                throw new AutoMapSharpConfigurationException(
                    $"ForProperty expression '{expression}' refers to a nested member. " +
                    "ForProperty only configures top-level members. Use ForPath(d => d.Parent.Child, ...) for nested members.");
            }

            return memberExpr.Member;
        }

        throw new ArgumentException($"Expression '{expression}' does not refer to a property or field.", nameof(expression));
    }
}
