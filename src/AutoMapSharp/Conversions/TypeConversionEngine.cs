using System;
using System.ComponentModel;
using System.Globalization;

namespace AutoMapSharp.Conversions;

/// <summary>
/// Provides built-in conversion strategies for primitives, nullables, enums, dates, and Guids.
/// </summary>
public static class TypeConversionEngine
{
    /// <summary>
    /// Attempts to convert a source value to the specified destination type.
    /// </summary>
    public static bool TryConvert(object? sourceValue, Type destinationType, out object? convertedValue)
    {
        convertedValue = null;

        if (destinationType == null)
        {
            throw new ArgumentNullException(nameof(destinationType));
        }

        if (sourceValue == null)
        {
            if (!destinationType.IsValueType || Nullable.GetUnderlyingType(destinationType) != null)
            {
                convertedValue = null;
                return true;
            }

            convertedValue = Activator.CreateInstance(destinationType);
            return true;
        }

        var sourceType = sourceValue.GetType();

        if (destinationType.IsAssignableFrom(sourceType))
        {
            convertedValue = sourceValue;
            return true;
        }

        var underlyingDest = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        var underlyingSource = Nullable.GetUnderlyingType(sourceType) ?? sourceType;

        if (underlyingDest.IsEnum)
        {
            if (sourceValue is string stringVal)
            {
                try
                {
                    convertedValue = Enum.Parse(underlyingDest, stringVal, ignoreCase: true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (IsNumericType(underlyingSource))
            {
                try
                {
                    convertedValue = Enum.ToObject(underlyingDest, sourceValue);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        if (underlyingSource.IsEnum)
        {
            if (underlyingDest == typeof(string))
            {
                convertedValue = sourceValue.ToString();
                return true;
            }

            if (IsNumericType(underlyingDest))
            {
                try
                {
                    convertedValue = Convert.ChangeType(sourceValue, underlyingDest, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        if (underlyingDest == typeof(Guid))
        {
            if (sourceValue is string guidString && Guid.TryParse(guidString, out var parsedGuid))
            {
                convertedValue = parsedGuid;
                return true;
            }
        }

        if (underlyingSource == typeof(Guid) && underlyingDest == typeof(string))
        {
            convertedValue = sourceValue.ToString();
            return true;
        }

        if (underlyingDest == typeof(DateTimeOffset) && sourceValue is DateTime dt)
        {
            convertedValue = new DateTimeOffset(dt);
            return true;
        }

        if (underlyingDest == typeof(DateTime) && sourceValue is DateTimeOffset dto)
        {
            convertedValue = dto.DateTime;
            return true;
        }

#if NET8_0_OR_GREATER
        if (underlyingDest == typeof(DateOnly))
        {
            if (sourceValue is DateTime d)
            {
                convertedValue = DateOnly.FromDateTime(d);
                return true;
            }
            if (sourceValue is DateTimeOffset doff)
            {
                convertedValue = DateOnly.FromDateTime(doff.DateTime);
                return true;
            }
            if (sourceValue is string s && DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly))
            {
                convertedValue = parsedDateOnly;
                return true;
            }
        }

        if (underlyingDest == typeof(TimeOnly))
        {
            if (sourceValue is DateTime d)
            {
                convertedValue = TimeOnly.FromDateTime(d);
                return true;
            }
            if (sourceValue is DateTimeOffset doff)
            {
                convertedValue = TimeOnly.FromDateTime(doff.DateTime);
                return true;
            }
            if (sourceValue is string s && TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTimeOnly))
            {
                convertedValue = parsedTimeOnly;
                return true;
            }
        }

        if (sourceValue is DateOnly dateOnly)
        {
            if (underlyingDest == typeof(DateTime))
            {
                convertedValue = dateOnly.ToDateTime(TimeOnly.MinValue);
                return true;
            }
            if (underlyingDest == typeof(string))
            {
                convertedValue = dateOnly.ToString("O", CultureInfo.InvariantCulture);
                return true;
            }
        }

        if (sourceValue is TimeOnly timeOnly)
        {
            if (underlyingDest == typeof(string))
            {
                convertedValue = timeOnly.ToString("O", CultureInfo.InvariantCulture);
                return true;
            }
        }
#endif

        try
        {
            convertedValue = Convert.ChangeType(sourceValue, underlyingDest, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            var converter = TypeDescriptor.GetConverter(underlyingDest);
            if (converter.CanConvertFrom(underlyingSource))
            {
                try
                {
                    convertedValue = converter.ConvertFrom(null, CultureInfo.InvariantCulture, sourceValue);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static bool IsNumericType(Type type)
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
                return true;
            default:
                return false;
        }
    }
}
