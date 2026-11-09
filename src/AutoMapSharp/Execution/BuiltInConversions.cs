using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapSharp.Conversions;
using AutoMapSharp.Exceptions;

namespace AutoMapSharp.Execution;

/// <summary>
/// Chooses a built-in conversion between two simple types when a plan is built, so mapping never
/// tries a conversion and catches the failure at run time.
/// </summary>
internal static class BuiltInConversions
{
    private static readonly Expression InvariantCulture = Expression.Constant(CultureInfo.InvariantCulture, typeof(IFormatProvider));

    /// <summary>
    /// True for types that are copied or converted as a single value rather than mapped member by member:
    /// primitives, enums, <see cref="string"/>, <see cref="decimal"/> and the other types in the <c>System</c> namespace
    /// such as <see cref="DateTime"/>, <see cref="Guid"/>, <see cref="TimeSpan"/> and <see cref="Uri"/>.
    /// </summary>
    public static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type.Namespace == "System";
    }

    /// <summary>
    /// Builds a conversion of <paramref name="value"/>, which is a non-null value of a non-nullable type,
    /// to the non-nullable type <paramref name="to"/>. Returns null when no built-in conversion applies.
    /// </summary>
    public static Expression? TryBuild(Expression value, Type to)
    {
        var from = value.Type;

        if (to == typeof(string))
        {
            return ToStringExpression(value);
        }

        if (to.IsEnum)
        {
            if (from == typeof(string))
            {
                return Expression.Call(typeof(EnumConversions).GetMethod(nameof(EnumConversions.Parse))!.MakeGenericMethod(to), value);
            }

            if (from.IsEnum)
            {
                return Expression.Call(typeof(EnumConversions).GetMethod(nameof(EnumConversions.MapByName))!.MakeGenericMethod(from, to), value);
            }

            if (IsNumeric(from))
            {
                var underlying = Enum.GetUnderlyingType(to);
                return Expression.Convert(NumericConvert(value, underlying), to);
            }

            return null;
        }

        if (from.IsEnum)
        {
            if (IsNumeric(to))
            {
                return NumericConvert(Expression.Convert(value, Enum.GetUnderlyingType(from)), to);
            }

            return null;
        }

        if (to == typeof(Guid) && from == typeof(string))
        {
            return Expression.Call(typeof(Guid).GetMethod(nameof(Guid.Parse), new[] { typeof(string) })!, value);
        }

        if (to == typeof(DateTimeOffset) && from == typeof(DateTime))
        {
            return Expression.New(typeof(DateTimeOffset).GetConstructor(new[] { typeof(DateTime) })!, value);
        }

        if (to == typeof(DateTime) && from == typeof(DateTimeOffset))
        {
            return Expression.Property(value, nameof(DateTimeOffset.DateTime));
        }

#if NET8_0_OR_GREATER
        if (to == typeof(DateOnly))
        {
            if (from == typeof(DateTime)) return Expression.Call(typeof(DateOnly).GetMethod(nameof(DateOnly.FromDateTime))!, value);
            if (from == typeof(DateTimeOffset)) return Expression.Call(typeof(DateOnly).GetMethod(nameof(DateOnly.FromDateTime))!, Expression.Property(value, nameof(DateTimeOffset.DateTime)));
            if (from == typeof(string)) return Expression.Call(typeof(DateOnly).GetMethod(nameof(DateOnly.Parse), new[] { typeof(string), typeof(IFormatProvider) })!, value, InvariantCulture);
        }

        if (to == typeof(TimeOnly))
        {
            if (from == typeof(DateTime)) return Expression.Call(typeof(TimeOnly).GetMethod(nameof(TimeOnly.FromDateTime))!, value);
            if (from == typeof(DateTimeOffset)) return Expression.Call(typeof(TimeOnly).GetMethod(nameof(TimeOnly.FromDateTime))!, Expression.Property(value, nameof(DateTimeOffset.DateTime)));
            if (from == typeof(string)) return Expression.Call(typeof(TimeOnly).GetMethod(nameof(TimeOnly.Parse), new[] { typeof(string), typeof(IFormatProvider) })!, value, InvariantCulture);
        }

        if (from == typeof(DateOnly) && to == typeof(DateTime))
        {
            return Expression.Call(value, typeof(DateOnly).GetMethod(nameof(DateOnly.ToDateTime), new[] { typeof(TimeOnly) })!, Expression.Constant(TimeOnly.MinValue));
        }
#endif

        // Same rules as Convert.ChangeType.
        var convertMethod = FindConvertMethod(from, to);
        if (convertMethod != null)
        {
            return convertMethod.GetParameters().Length == 2
                ? Expression.Call(convertMethod, value, InvariantCulture)
                : Expression.Call(convertMethod, value);
        }

        var op = FindConversionOperator(from, to);
        if (op != null)
        {
            return Expression.Convert(value, to, op);
        }

        if (IsSimple(to))
        {
            var converter = TypeDescriptor.GetConverter(to);
            if (converter.CanConvertFrom(from))
            {
                return Expression.Call(
                    typeof(BuiltInConversions).GetMethod(nameof(ConvertWithTypeConverter), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(to),
                    Expression.Constant(converter),
                    Expression.Convert(value, typeof(object)));
            }
        }

        return null;
    }

    private static Expression ToStringExpression(Expression value)
    {
        var from = value.Type;

#if NET8_0_OR_GREATER
        if (from == typeof(DateOnly) || from == typeof(TimeOnly))
        {
            return Expression.Call(value, from.GetMethod("ToString", new[] { typeof(string), typeof(IFormatProvider) })!, Expression.Constant("O"), InvariantCulture);
        }
#endif

        if (!from.IsEnum)
        {
            var convertToString = typeof(Convert).GetMethod(nameof(Convert.ToString), new[] { from, typeof(IFormatProvider) });
            if (convertToString != null && convertToString.GetParameters()[0].ParameterType == from)
            {
                return Expression.Call(convertToString, value, InvariantCulture);
            }
        }

        var toString = from.GetMethod(nameof(ToString), Type.EmptyTypes) ?? typeof(object).GetMethod(nameof(ToString))!;
        return from.IsValueType && toString.DeclaringType == typeof(object)
            ? Expression.Call(Expression.Convert(value, typeof(object)), toString)
            : Expression.Call(value, toString);
    }

    private static MethodInfo? FindConvertMethod(Type from, Type to)
    {
        var code = Type.GetTypeCode(to);
        if (code is TypeCode.Object or TypeCode.Empty or TypeCode.DBNull or TypeCode.String)
        {
            return null;
        }

        if (!typeof(IConvertible).IsAssignableFrom(from) || AlwaysThrows(Type.GetTypeCode(from), code))
        {
            return null;
        }

        var name = "To" + to.Name;
        var exact = typeof(Convert).GetMethod(name, new[] { from, typeof(IFormatProvider) });
        if (exact != null && exact.GetParameters()[0].ParameterType == from)
        {
            return exact;
        }

        exact = typeof(Convert).GetMethod(name, new[] { from });
        if (exact != null && exact.GetParameters()[0].ParameterType == from)
        {
            return exact;
        }

        return null;
    }

    private static bool AlwaysThrows(TypeCode from, TypeCode to)
    {
        if (from == to || from == TypeCode.String || to == TypeCode.String) return false;
        if (from == TypeCode.DateTime || to == TypeCode.DateTime) return true;
        if (from == TypeCode.Char) return to is TypeCode.Boolean or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
        if (to == TypeCode.Char) return from is TypeCode.Boolean or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
        return false;
    }

    private static MethodInfo? FindConversionOperator(Type from, Type to)
    {
        foreach (var type in new[] { from, to })
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if ((method.Name == "op_Implicit" || method.Name == "op_Explicit") &&
                    method.ReturnType == to &&
                    method.GetParameters() is { Length: 1 } parameters &&
                    parameters[0].ParameterType == from)
                {
                    return method;
                }
            }
        }

        return null;
    }

    internal static Expression NumericConvert(Expression value, Type to)
    {
        if (value.Type == to)
        {
            return value;
        }

        var method = FindConvertMethod(value.Type, to);
        return method != null ? Expression.Call(method, value) : Expression.ConvertChecked(value, to);
    }

    private static bool IsNumeric(Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return !type.IsEnum;
            default:
                return false;
        }
    }

    private static T ConvertWithTypeConverter<T>(TypeConverter converter, object value)
    {
        return (T)converter.ConvertFrom(null, CultureInfo.InvariantCulture, value)!;
    }

    /// <summary>
    /// Last-resort conversion when the static types give no plan, for example a source member typed <see cref="object"/>.
    /// Throws a clear error when the runtime value cannot be converted.
    /// </summary>
    internal static T RuntimeConvert<T>(object? value)
    {
        if (value == null)
        {
            return default!;
        }

        if (value is T typed)
        {
            return typed;
        }

        if (TypeConversionEngine.TryConvert(value, typeof(T), out var converted) && converted is T result)
        {
            return result;
        }

        throw new AutoMapSharpMappingException(
            $"A value of type '{value.GetType().FullName}' cannot be converted to '{typeof(T).FullName}'. Define a map or a converter for this pair.",
            value.GetType(),
            typeof(T));
    }
}

/// <summary>
/// Enum conversions used by compiled plans.
/// </summary>
internal static class EnumConversions
{
    public static TEnum Parse<TEnum>(string value) where TEnum : struct
    {
        return (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase: true);
    }

    /// <summary>
    /// Maps one enum to another by member name, ignoring case. A value whose name does not exist in the
    /// destination enum, or a combination of flags, is converted by its numeric value instead.
    /// </summary>
    public static TTo MapByName<TFrom, TTo>(TFrom value) where TFrom : struct where TTo : struct
    {
        return EnumMap<TFrom, TTo>.ByName.TryGetValue(value, out var mapped)
            ? mapped
            : (TTo)Enum.ToObject(typeof(TTo), Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(TFrom)), CultureInfo.InvariantCulture));
    }

    private static class EnumMap<TFrom, TTo> where TFrom : struct where TTo : struct
    {
        public static readonly Dictionary<TFrom, TTo> ByName = Build();

        private static Dictionary<TFrom, TTo> Build()
        {
            var map = new Dictionary<TFrom, TTo>();
            var destinationNames = Enum.GetNames(typeof(TTo));
            foreach (var name in Enum.GetNames(typeof(TFrom)))
            {
                foreach (var destinationName in destinationNames)
                {
                    if (string.Equals(name, destinationName, StringComparison.OrdinalIgnoreCase))
                    {
                        map[(TFrom)Enum.Parse(typeof(TFrom), name)] = (TTo)Enum.Parse(typeof(TTo), destinationName);
                        break;
                    }
                }
            }

            return map;
        }
    }
}
