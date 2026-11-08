using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Projections;

namespace AutoMapSharp.Execution;

/// <summary>
/// Builds the expression tree that maps one source type to one destination type.
/// </summary>
/// <remarks>
/// The plan has the shape <c>(source, destination, useDestination, context) =&gt; destination</c>.
/// Every decision that depends only on types, such as which source member feeds a destination member,
/// which conversion applies, and which constructor to call, is made here once. The compiled plan only
/// reads, converts and assigns.
/// </remarks>
internal sealed class PlanBuilder
{
    private static readonly MethodInfo RegisterMethod = typeof(ResolutionContext).GetMethod(
        nameof(ResolutionContext.RegisterMappedInstance), new[] { typeof(object), typeof(Type), typeof(object) })!;

    private static readonly MethodInfo MemberErrorMethod = typeof(PlanBuilder).GetMethod(nameof(MemberError), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo RuntimeConvertMethod = typeof(BuiltInConversions).GetMethod(nameof(BuiltInConversions.RuntimeConvert), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly MapEngine _engine;
    private readonly TypeMappingDescriptor? _map;
    private readonly Type _sourceType;
    private readonly Type _destinationType;
    private readonly ParameterExpression _source;
    private readonly ParameterExpression _destinationIn;
    private readonly ParameterExpression _useDestination;
    private readonly ParameterExpression _context;
    private readonly List<string> _stages = new();
    private ParameterExpression? _stage;

    private PlanBuilder(MapEngine engine, TypeMappingDescriptor? map, Type sourceType, Type destinationType)
    {
        _engine = engine;
        _map = map;
        _sourceType = sourceType;
        _destinationType = destinationType;
        _source = Expression.Parameter(sourceType, "source");
        _destinationIn = Expression.Parameter(destinationType, "destination");
        _useDestination = Expression.Parameter(typeof(bool), "useDestination");
        _context = Expression.Parameter(typeof(ResolutionContext), "context");
    }

    private bool AllowNullCollections => _map?.AllowNullCollections ?? false;

    public static Expression<Func<TSource, TDestination, bool, ResolutionContext, TDestination>> Build<TSource, TDestination>(MapEngine engine, TypeMappingDescriptor? map)
    {
        var builder = new PlanBuilder(engine, map, typeof(TSource), typeof(TDestination));
        var body = builder.BuildBody();
        return Expression.Lambda<Func<TSource, TDestination, bool, ResolutionContext, TDestination>>(
            body, builder._source, builder._destinationIn, builder._useDestination, builder._context);
    }

    private Expression BuildBody()
    {
        // String-keyed dictionaries and ExpandoObject, in either direction.
        if (DictionaryMapping.IsStringObjectDictionary(_sourceType) && !BuiltInConversions.IsSimple(_destinationType) &&
            !CollectionPlanner.IsEnumerable(_destinationType))
        {
            return DictionaryMapping.BuildFromDictionary(_engine, _map, _source, _destinationType, _destinationIn, _useDestination, _context);
        }

        if (DictionaryMapping.IsStringObjectDictionary(_destinationType) && !BuiltInConversions.IsSimple(_sourceType) &&
            !CollectionPlanner.IsEnumerable(_sourceType))
        {
            return DictionaryMapping.BuildToDictionary(_map, _source, _destinationType, _destinationIn, _useDestination);
        }

        if (CollectionPlanner.IsCollection(_destinationType) && CollectionPlanner.IsEnumerable(_sourceType))
        {
            Expression? existing = _destinationType.IsValueType
                ? null
                : Expression.Condition(_useDestination, _destinationIn, Expression.Constant(null, _destinationType));
            return CollectionPlanner.Build(_source, _destinationType, existing, _context, AllowNullCollections, BuildElementConverter, MergeLookup);
        }

        if (BuiltInConversions.IsSimple(_destinationType))
        {
            return ConvertValue(_source, _destinationType, null, _context, skipExplicitMap: true);
        }

        return BuildObject();
    }

    // ------------------------------------------------------------------
    // Object plans
    // ------------------------------------------------------------------

    private Expression BuildObject()
    {
        var destination = Expression.Variable(_destinationType, "d");
        _stage = Expression.Variable(typeof(int), "stage");
        var body = new List<Expression>();

        // 1. Use the given destination, or create one.
        SetStage("constructor");
        var (construct, constructorMembers) = BuildConstruction();
        Expression useExisting = _destinationType.IsValueType
            ? _useDestination
            : Expression.AndAlso(_useDestination, Expression.NotEqual(_destinationIn, Expression.Constant(null, _destinationType)));
        body.Add(Expression.Assign(_stage, Expression.Constant(0)));
        body.Add(Expression.Assign(destination, Expression.Condition(useExisting, _destinationIn, construct)));

        // 2. Register the new object before mapping members, so circular references resolve to it.
        if (_map?.PreserveReferences == true)
        {
            body.Add(Expression.Call(_context, RegisterMethod,
                Expression.Convert(_source, typeof(object)), Expression.Constant(_destinationType, typeof(Type)), Expression.Convert(destination, typeof(object))));
        }

        // 3. BeforeMap callbacks, base map first, then a per-call BeforeMap passed to Map().
        if (_map != null)
        {
            foreach (var action in _map.BeforeMapActions)
            {
                body.Add(SetStageExpression("BeforeMap"));
                body.Add(InvokeCallback(action, destination));
            }
        }

        body.Add(SetStageExpression("BeforeMap"));
        body.Add(OperationCallback(nameof(ResolutionContext.OnBeforeMap), destination));

        // 4. Members, in declaration order, so a resolver can read members mapped before it.
        foreach (var member in SourceMemberMatcher.GetDestinationMembers(_destinationType, _map))
        {
            if (constructorMembers.Contains(member.Name))
            {
                continue;
            }

            var assignment = BuildMember(member, destination);
            if (assignment != null)
            {
                body.Add(SetStageExpression(member.Name));
                body.Add(assignment);
            }
        }

        // 5. Nested members configured with ForPath, including reverse-map unflattening.
        if (_map != null)
        {
            foreach (var path in _map.PathRules)
            {
                var assignment = BuildPath(path, destination);
                if (assignment != null)
                {
                    body.Add(SetStageExpression(path.Key));
                    body.Add(assignment);
                }
            }

            // 6. AfterMap callbacks, base map first.
            foreach (var action in _map.AfterMapActions)
            {
                body.Add(SetStageExpression("AfterMap"));
                body.Add(InvokeCallback(action, destination));
            }
        }

        body.Add(SetStageExpression("AfterMap"));
        body.Add(OperationCallback(nameof(ResolutionContext.OnAfterMap), destination));

        body.Add(destination);

        // One try block for the whole plan. The stage variable names the member that failed.
        var exception = Expression.Variable(typeof(Exception), "ex");
        var guarded = Expression.TryCatch(
            Expression.Block(_destinationType, body),
            Expression.Catch(exception, Expression.Block(
                _destinationType,
                Expression.IfThen(Expression.TypeIs(exception, typeof(AutoMapSharpException)), Expression.Rethrow()),
                Expression.Throw(
                    Expression.Call(MemberErrorMethod, exception, Expression.Constant(_sourceType), Expression.Constant(_destinationType),
                        Expression.ArrayIndex(Expression.Constant(_stages.ToArray()), _stage)),
                    _destinationType))));

        return Expression.Block(_destinationType, new[] { destination, _stage }, guarded);
    }

    private void SetStage(string name) => _stages.Add(name);

    private Expression SetStageExpression(string name)
    {
        _stages.Add(name);
        return Expression.Assign(_stage!, Expression.Constant(_stages.Count - 1));
    }

    private Expression OperationCallback(string method, Expression destination) =>
        Expression.IfThen(
            Expression.Field(_context, nameof(ResolutionContext.HasOperationCallbacks)),
            Expression.Call(_context, typeof(ResolutionContext).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!,
                Expression.Convert(_source, typeof(object)), Expression.Convert(destination, typeof(object))));

    private Expression InvokeCallback(Delegate callback, Expression destination)
    {
        var parameters = callback.GetType().GetMethod("Invoke")!.GetParameters();
        return Expression.Invoke(
            Expression.Constant(callback),
            ConvertIfNeeded(_source, parameters[0].ParameterType),
            ConvertIfNeeded(destination, parameters[1].ParameterType),
            _context);
    }

    private Expression? BuildMember(MemberInfo member, Expression destination)
    {
        PropertyRule? rule = null;
        _map?.PropertyRules.TryGetValue(member.Name, out rule);
        if (rule?.IsIgnored == true)
        {
            return null;
        }

        var memberType = SourceMemberMatcher.MemberType(member);
        var canRead = member is FieldInfo || (member is PropertyInfo p && p.GetGetMethod(true) != null);
        var target = Expression.MakeMemberAccess(destination, member);

        // The destination's current value is mapped into, unless DoNotUseDestinationValue is set.
        Expression? existing = !memberType.IsValueType && canRead && rule?.UseDestinationValue != false ? target : null;

        var value = BuildValue(rule, member.Name, memberType, destination, existing, useConventions: true);
        if (value == null)
        {
            return null;
        }

        Expression assignment;
        var keepExisting = rule?.UseDestinationValue == true && existing != null;
        if (rule?.ValueCondition != null || keepExisting)
        {
            // Resolve into a temporary first, so a value condition can see it and a null value can keep the existing one.
            var temp = Expression.Variable(memberType, "value");
            Expression assign = Expression.Assign(target, temp);
            if (keepExisting)
            {
                assign = Expression.IfThen(Expression.NotEqual(temp, Expression.Constant(null, memberType)), assign);
            }

            if (rule?.ValueCondition != null)
            {
                assign = Expression.IfThen(InvokeValueCondition(rule.ValueCondition, destination, temp, canRead ? target : Expression.Default(memberType)), assign);
            }

            assignment = Expression.Block(typeof(void), new[] { temp }, Expression.Assign(temp, value), assign);
        }
        else
        {
            assignment = Expression.Assign(target, value);
        }

        if (rule?.Condition != null)
        {
            assignment = Expression.IfThen(InvokeCondition(rule.Condition, destination), assignment);
        }

        return assignment;
    }

    private Expression InvokeValueCondition(Delegate condition, Expression destination, Expression newValue, Expression currentValue)
    {
        var parameters = condition.GetType().GetMethod("Invoke")!.GetParameters();
        var arguments = new List<Expression>
        {
            ConvertIfNeeded(_source, parameters[0].ParameterType),
            ConvertIfNeeded(destination, parameters[1].ParameterType),
            ConvertIfNeeded(newValue, parameters[2].ParameterType)
        };

        if (parameters.Length == 4)
        {
            arguments.Add(ConvertIfNeeded(currentValue, parameters[3].ParameterType));
        }

        return Expression.Invoke(Expression.Constant(condition), arguments);
    }

    private Expression? BuildPath(PathRule path, Expression destination)
    {
        if (path.Rule.IsIgnored)
        {
            return null;
        }

        if (!FrozenConfiguration.CanCreatePath(path.Path))
        {
            throw new AutoMapSharpConfigurationException(
                $"ForPath '{path.Key}' on the map from '{_sourceType.FullName}' to '{_destinationType.FullName}' cannot be set. " +
                "Every intermediate member needs a getter, a setter and a class type with a parameterless constructor, and the last member needs a setter.");
        }

        var leaf = path.Path[path.Path.Count - 1];
        var value = BuildValue(path.Rule, leaf.Name, leaf.PropertyType, destination, null, useConventions: false);
        if (value == null)
        {
            return null;
        }

        var variables = new List<ParameterExpression>();
        var steps = new List<Expression>();
        Expression current = destination;
        for (int i = 0; i < path.Path.Count - 1; i++)
        {
            var step = path.Path[i];
            var temp = Expression.Variable(step.PropertyType, step.Name);
            variables.Add(temp);
            steps.Add(Expression.Assign(temp, Expression.Property(current, step)));
            steps.Add(Expression.IfThen(
                Expression.Equal(temp, Expression.Constant(null, step.PropertyType)),
                Expression.Block(
                    Expression.Assign(temp, Expression.New(step.PropertyType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!)),
                    Expression.Assign(Expression.Property(current, step), temp))));
            current = temp;
        }

        steps.Add(Expression.Assign(Expression.Property(current, leaf), value));
        Expression assignment = Expression.Block(typeof(void), variables, steps);

        if (path.Rule.Condition != null)
        {
            assignment = Expression.IfThen(InvokeCondition(path.Rule.Condition, destination), assignment);
        }

        return assignment;
    }

    private Expression InvokeCondition(Delegate condition, Expression destination)
    {
        var parameters = condition.GetType().GetMethod("Invoke")!.GetParameters();
        return Expression.Invoke(
            Expression.Constant(condition),
            ConvertIfNeeded(_source, parameters[0].ParameterType),
            ConvertIfNeeded(destination, parameters[1].ParameterType));
    }

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    private (Expression Construct, HashSet<string> ConstructorMembers) BuildConstruction()
    {
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_map?.ConstructorExpression is { } factory)
        {
            var parameters = factory.GetType().GetMethod("Invoke")!.GetParameters();
            var factoryArguments = parameters.Length == 2
                ? new[] { ConvertIfNeeded(_source, parameters[0].ParameterType), (Expression)_context }
                : new[] { ConvertIfNeeded(_source, parameters[0].ParameterType) };
            return (ConvertIfNeeded(Expression.Invoke(Expression.Constant(factory), factoryArguments), _destinationType), none);
        }

        if (_destinationType.IsAbstract || _destinationType.IsInterface)
        {
            return (ThrowMapping($"Cannot create an instance of abstract type '{_destinationType.FullName}'. Use Include, ConstructUsing or a concrete destination type."), none);
        }

        var constructors = _destinationType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var withParameters = constructors.Where(c => c.GetParameters().Length > 0).OrderByDescending(c => c.GetParameters().Length).ToList();

        if (_destinationType.IsValueType)
        {
            // A struct with settable members starts from its default value. A struct without any, such as a
            // readonly struct, needs its constructor.
            if (SourceMemberMatcher.GetDestinationProperties(_destinationType).Length > 0 || withParameters.Count == 0)
            {
                return (Expression.New(_destinationType), none);
            }
        }
        else
        {
            var parameterless = constructors.FirstOrDefault(c => c.GetParameters().Length == 0 && c.IsPublic)
                                ?? constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
            if (parameterless != null)
            {
                return (Expression.New(parameterless), none);
            }

            if (withParameters.Count == 0)
            {
                return (ThrowMapping($"Type '{_destinationType.FullName}' has no constructor that can be used."), none);
            }
        }

        var chosen = withParameters.FirstOrDefault(c => c.GetParameters().All(CanResolveParameter)) ?? withParameters[0];
        var nullDestination = Expression.Default(_destinationType);
        var arguments = chosen.GetParameters().Select(p => BuildConstructorArgument(p, nullDestination)).ToArray();
        var members = new HashSet<string>(chosen.GetParameters().Select(p => p.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase);
        return (Expression.New(chosen, arguments), members);
    }

    private bool CanResolveParameter(ParameterInfo parameter)
    {
        if (parameter.Name == null) return parameter.HasDefaultValue;
        if (_map != null && _map.CtorParamRules.ContainsKey(parameter.Name)) return true;
        if (_map != null && _map.PropertyRules.TryGetValue(parameter.Name, out var rule) && !rule.IsIgnored) return true;
        return SourceMemberMatcher.Find(_sourceType, parameter.Name, _map) != null || parameter.HasDefaultValue;
    }

    private Expression BuildConstructorArgument(ParameterInfo parameter, Expression nullDestination)
    {
        Expression? value = null;
        if (parameter.Name != null && _map != null && _map.CtorParamRules.TryGetValue(parameter.Name, out var ctorRule))
        {
            if (ctorRule.HasConstantValue)
            {
                return ConvertIfNeeded(Expression.Constant(ctorRule.ConstantValue, ConstantType(ctorRule.ConstantValue, parameter.ParameterType)), parameter.ParameterType);
            }

            if (ctorRule.SourceExpression != null)
            {
                return ConvertValue(InlineSourceExpression(ctorRule.SourceExpression), parameter.ParameterType, null, _context);
            }
        }

        if (parameter.Name != null)
        {
            PropertyRule? rule = null;
            _map?.PropertyRules.TryGetValue(parameter.Name, out rule);
            if (rule?.IsIgnored != true)
            {
                value = BuildValue(rule, parameter.Name, parameter.ParameterType, nullDestination, null, useConventions: true);
            }
        }

        if (value != null)
        {
            return value;
        }

        if (parameter.HasDefaultValue && parameter.DefaultValue != null && parameter.DefaultValue is not DBNull)
        {
            return ConvertIfNeeded(Expression.Constant(parameter.DefaultValue), parameter.ParameterType);
        }

        return Expression.Default(parameter.ParameterType);
    }

    // ------------------------------------------------------------------
    // Values
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds the value for one destination member or constructor parameter, already converted to <paramref name="memberType"/>,
    /// or returns null when the member has no source and should be left alone.
    /// </summary>
    private Expression? BuildValue(PropertyRule? rule, string memberName, Type memberType, Expression destination, Expression? existing, bool useConventions)
    {
        if (rule?.HasConstantValue == true)
        {
            return ApplyTransformers(ConvertIfNeeded(Expression.Constant(rule.ConstantValue, ConstantType(rule.ConstantValue, memberType)), memberType), memberType, rule);
        }

        Expression? raw = null;
        if (rule?.CustomResolverType != null)
        {
            raw = BuildResolverCall(rule.CustomResolverType, destination);
        }
        else if (rule?.SourceExpression != null)
        {
            raw = InlineSourceExpression(rule.SourceExpression);
        }
        else if (rule?.SourceMemberName != null)
        {
            var named = SourceMemberMatcher.GetSourceMembers(_sourceType, _map)
                .FirstOrDefault(m => string.Equals(m.Name, rule.SourceMemberName, StringComparison.OrdinalIgnoreCase))
                ?? throw new AutoMapSharpConfigurationException(
                    $"Type '{_sourceType.FullName}' has no public property or field named '{rule.SourceMemberName}', used by MapSource for member '{memberName}'.");
            raw = Expression.MakeMemberAccess(_source, named);
        }
        else if (useConventions)
        {
            var access = SourceMemberMatcher.Find(_sourceType, memberName, _map);
            if (access != null)
            {
                raw = SourceMemberMatcher.BuildRead(_source, access);
            }
        }

        if (raw == null)
        {
            return rule?.HasFallbackValue == true
                ? ApplyTransformers(ConvertIfNeeded(Expression.Constant(rule.FallbackValue, ConstantType(rule.FallbackValue, memberType)), memberType), memberType, rule)
                : null;
        }

        if (rule?.ValueConverterType != null)
        {
            raw = BuildValueConverterCall(rule.ValueConverterType, raw, memberName);
        }

        Expression converted;
        if (rule?.HasFallbackValue == true && CanBeNull(raw.Type))
        {
            var temp = Expression.Variable(raw.Type, "raw");
            converted = Expression.Block(
                memberType,
                new[] { temp },
                Expression.Assign(temp, raw),
                Expression.Condition(
                    Expression.Equal(temp, Expression.Constant(null, raw.Type)),
                    ConvertIfNeeded(Expression.Constant(rule.FallbackValue, ConstantType(rule.FallbackValue, memberType)), memberType),
                    ConvertValue(temp, memberType, existing, _context)));
        }
        else
        {
            converted = ConvertValue(raw, memberType, existing, _context);
        }

        return ApplyTransformers(converted, memberType, rule);
    }

    private Expression BuildValueConverterCall(Type converterType, Expression raw, string memberName)
    {
        var converterInterface = converterType.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueConverter<,>))
            ?? throw new AutoMapSharpConfigurationException($"Value converter '{converterType.FullName}' does not implement IValueConverter<TSourceMember, TDestinationMember>.");

        var sourceMemberType = converterInterface.GetGenericArguments()[0];
        var factory = new InstanceFactory(converterType);
        return Expression.Call(
            Expression.Convert(factory.CreateExpression(_context), converterInterface),
            converterInterface.GetMethod("Convert")!,
            ConvertValue(raw, sourceMemberType, null, _context),
            _context);
    }

    private static Type ConstantType(object? value, Type memberType) =>
        value == null || memberType.IsInstanceOfType(value) ? memberType : value.GetType();

    private Expression InlineSourceExpression(LambdaExpression lambda)
    {
        var parameter = lambda.Parameters[0];
        var body = NullSafeRewriter.Rewrite(lambda);
        return ParameterReplacer.Replace(body, parameter, ConvertIfNeeded(_source, parameter.Type));
    }

    private Expression BuildResolverCall(Type resolverType, Expression destination)
    {
        var resolverInterface = resolverType.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IPropertyResolver<,,>) &&
            i.GetGenericArguments()[0].IsAssignableFrom(_sourceType) &&
            i.GetGenericArguments()[1].IsAssignableFrom(_destinationType))
            ?? throw new AutoMapSharpConfigurationException(
                $"Resolver type '{resolverType.FullName}' does not implement IPropertyResolver<{_sourceType.Name}, {_destinationType.Name}, TMember>.");

        var args = resolverInterface.GetGenericArguments();
        var factory = new InstanceFactory(resolverType);
        return Expression.Call(
            Expression.Convert(factory.CreateExpression(_context), resolverInterface),
            resolverInterface.GetMethod("Resolve")!,
            ConvertIfNeeded(_source, args[0]),
            ConvertIfNeeded(destination, args[1]),
            _context);
    }

    private Expression ApplyTransformers(Expression value, Type memberType, PropertyRule? rule = null)
    {
        var transformers = (_map?.ValueTransformers.GetTransformers(memberType) ?? Array.Empty<Delegate>())
            .Concat(rule?.Transformers ?? Array.Empty<Delegate>());

        foreach (var transformer in transformers)
        {
            if (transformer.GetType() == typeof(Func<,>).MakeGenericType(memberType, memberType))
            {
                value = Expression.Invoke(Expression.Constant(transformer), value);
            }
        }

        return value;
    }

    /// <summary>
    /// For an element map with EqualityComparison, returns the comparison and a function that maps onto an existing item.
    /// </summary>
    private (Delegate Comparison, Delegate MapOnto)? MergeLookup(Type sourceElement, Type destinationElement)
    {
        var elementMap = _engine.Configuration.Find(sourceElement, destinationElement);
        if (elementMap?.EqualityComparison is not { } equals)
        {
            return null;
        }

        var holder = _engine.GetHolder(sourceElement, destinationElement);
        var source = Expression.Parameter(sourceElement, "source");
        var destination = Expression.Parameter(destinationElement, "destination");
        var context = Expression.Parameter(typeof(ResolutionContext), "context");
        var call = Expression.Call(Expression.Constant(holder), holder.GetType().GetMethod(nameof(MapperHolder<object, object>.Map))!,
            source, destination, Expression.Constant(true), context);
        var mapOnto = Expression.Lambda(typeof(Func<,,,>).MakeGenericType(sourceElement, destinationElement, typeof(ResolutionContext), destinationElement),
            call, source, destination, context);
        return (equals, PlanCompiler.Compile(mapOnto));
    }

    private LambdaExpression BuildElementConverter(Type sourceElement, Type destinationElement)
    {
        var item = Expression.Parameter(sourceElement, "item");
        var context = Expression.Parameter(typeof(ResolutionContext), "context");
        var body = ConvertValue(item, destinationElement, null, context);
        return Expression.Lambda(typeof(Func<,,>).MakeGenericType(sourceElement, typeof(ResolutionContext), destinationElement), body, item, context);
    }

    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="targetType"/>. The strategy is chosen from the static types:
    /// collection mapping, assignment, an explicitly defined map, nullable unwrapping, a built-in conversion,
    /// a nested object map, and finally a run-time conversion.
    /// </summary>
    private Expression ConvertValue(Expression value, Type targetType, Expression? existing, Expression context, bool skipExplicitMap = false)
    {
        var valueType = value.Type;

        if (CollectionPlanner.IsCollection(targetType) && CollectionPlanner.IsEnumerable(valueType))
        {
            return CollectionPlanner.Build(value, targetType, existing, context, AllowNullCollections, BuildElementConverter, MergeLookup);
        }

        if (valueType == targetType && !skipExplicitMap)
        {
            return _engine.HasExplicitMap(valueType, targetType) && !BuiltInConversions.IsSimple(targetType)
                ? GuardNull(value, targetType, v => NestedMap(v, targetType, existing, context))
                : value;
        }

        if (valueType == targetType)
        {
            return value;
        }

        if (!skipExplicitMap && _engine.HasExplicitMap(valueType, targetType))
        {
            return GuardNull(value, targetType, v => NestedMap(v, targetType, existing, context));
        }

        if (targetType.IsAssignableFrom(valueType))
        {
            return Expression.Convert(value, targetType);
        }

        if (Nullable.GetUnderlyingType(valueType) is { } underlyingValue)
        {
            var temp = Expression.Variable(valueType, "nullable");
            return Expression.Block(
                targetType,
                new[] { temp },
                Expression.Assign(temp, value),
                Expression.Condition(
                    Expression.Property(temp, "HasValue"),
                    ConvertValue(Expression.Property(temp, "Value"), targetType, existing, context, skipExplicitMap),
                    Expression.Default(targetType)));
        }

        if (Nullable.GetUnderlyingType(targetType) is { } underlyingTarget)
        {
            return GuardNull(value, targetType, v => Expression.Convert(ConvertValue(v, underlyingTarget, null, context, skipExplicitMap), targetType));
        }

        return GuardNull(value, targetType, v => ConvertNonNull(v, targetType, existing, context));
    }

    private Expression ConvertNonNull(Expression value, Type targetType, Expression? existing, Expression context)
    {
        var builtIn = BuiltInConversions.TryBuild(value, targetType);
        if (builtIn != null)
        {
            return builtIn;
        }

        if (!BuiltInConversions.IsSimple(targetType) && targetType != typeof(object))
        {
            return NestedMap(value, targetType, existing, context);
        }

        return Expression.Call(RuntimeConvertMethod.MakeGenericMethod(targetType), Expression.Convert(value, typeof(object)));
    }

    /// <summary>
    /// For a value that can be null, yields the default of <paramref name="targetType"/> when it is null and
    /// <paramref name="convert"/> otherwise.
    /// </summary>
    private static Expression GuardNull(Expression value, Type targetType, Func<Expression, Expression> convert)
    {
        if (value.Type.IsValueType)
        {
            return convert(value);
        }

        var temp = Expression.Variable(value.Type, "value");
        return Expression.Block(
            targetType,
            new[] { temp },
            Expression.Assign(temp, value),
            Expression.Condition(Expression.Equal(temp, Expression.Constant(null, value.Type)), Expression.Default(targetType), convert(temp)));
    }

    private Expression NestedMap(Expression value, Type targetType, Expression? existing, Expression context)
    {
        var holder = _engine.GetHolder(value.Type, targetType);
        var mapMethod = holder.GetType().GetMethod(nameof(MapperHolder<object, object>.Map))!;
        Expression destination = existing != null ? ConvertIfNeeded(existing, targetType) : Expression.Default(targetType);
        Expression useDestination = existing != null && !targetType.IsValueType
            ? Expression.NotEqual(destination, Expression.Constant(null, targetType))
            : Expression.Constant(false);
        return Expression.Call(Expression.Constant(holder), mapMethod, value, destination, useDestination, context);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static bool CanBeNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    private static Expression ConvertIfNeeded(Expression expression, Type type) =>
        expression.Type == type ? expression : Expression.Convert(expression, type);

    private Expression ThrowMapping(string message) =>
        Expression.Throw(
            Expression.New(
                typeof(AutoMapSharpMappingException).GetConstructor(new[] { typeof(string), typeof(Type), typeof(Type), typeof(Exception) })!,
                Expression.Constant(message),
                Expression.Constant(_sourceType, typeof(Type)),
                Expression.Constant(_destinationType, typeof(Type)),
                Expression.Constant(null, typeof(Exception))),
            _destinationType);

    private static Exception MemberError(Exception inner, Type sourceType, Type destinationType, string stage)
    {
        var what = stage switch
        {
            "constructor" => "creating the destination object",
            "BeforeMap" => "running a BeforeMap callback",
            "AfterMap" => "running an AfterMap callback",
            _ => $"mapping member '{stage}'"
        };

        return new AutoMapSharpMappingException(
            $"Error {what} from '{sourceType.FullName}' to '{destinationType.FullName}': {inner.Message}",
            sourceType, destinationType, inner);
    }
}
