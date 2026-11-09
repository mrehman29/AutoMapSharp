# Built-in Type Conversions

## Overview
AutoMapSharp includes automatic conversions for common .NET types without requiring custom value resolvers.

## Supported Built-In Conversions

### 1. Enums
* `Enum` <-> `string` (Case-insensitive parsing)
* `Enum` <-> `int` (and any underlying numeric type: byte, short, long)

```csharp
public enum Status { Pending, Active, Completed }

// Automatically maps Status.Active -> "Active" and "active" -> Status.Active
```

### 2. Guids
* `Guid` <-> `string` (Formats to standard hyphenated 36-character string and parses valid GUID strings)

### 3. Date & Time
* `DateTime` <-> `DateTimeOffset`
* `DateTime` / `DateTimeOffset` <-> `DateOnly` / `TimeOnly` (on .NET 8 and .NET 9)
* Standard ISO 8601 string formatting and parsing

### 4. Nullable & Primitives
* Implicit widening: `int` -> `long`, `float` -> `double`
* Nullable wrapping: `T` -> `T?`
* Nullable unwrapping: `T?` -> `T` (assigns `default(T)` or fallback value if null)

### 5. Values that already have the destination type

When the source value can already be assigned to the destination property, AutoMapSharp assigns it as is. This covers:

* Nullable value types such as `int?`, `decimal?`, `DateTime?` and `Guid?`.
* Structs such as `TimeSpan`, `DateOnly`, `TimeOnly`, and your own structs and record structs.
* `object`-typed properties, which receive the same instance.
* Nested objects of the same type, which share the same reference.

To copy a nested object instead of sharing it, define a map for that type:

```csharp
DefineMap<Address, Address>(); // nested Address values are now copied member by member
```

### 6. How a conversion is chosen

The conversion is chosen once, when the plan for a type pair is built, in this order:

1. Collection to collection.
2. Same type, or an explicitly defined map for the pair.
3. Assignable types, assigned as is.
4. Nullable values are unwrapped. `null` gives `null`, or the default value for a non-nullable destination.
5. A built-in conversion: enums, `Guid`, dates, `System.Convert` rules for numbers, booleans and strings with the invariant culture, user-defined conversion operators, and `TypeConverter`.
6. A nested object map, for classes and structs.
7. A run-time conversion, used only when the static types give no answer, such as a member typed `object`. It throws an `AutoMapSharpMappingException` when the value cannot be converted.

Enum rules are described in [Enum Mapping](enum-mapping.md).

## Migrating from AutoMapper

| Conversion | AutoMapper Default | AutoMapSharp Default |
|---|---|---|
| `Enum` -> `string` | Built-in `.ToString()` | Built-in `.ToString()` |
| `string` -> `Enum` | Case-insensitive `Enum.Parse` | Case-insensitive `Enum.Parse` |
| `Guid` -> `string` | Built-in | Built-in |
| `DateTime` -> `DateOnly` | Requires custom type converter on older versions | Built-in on modern .NET |
| Same-type value or reference | Assigned as is | Assigned as is |
| Enum -> different enum | By name, then by value | By name, then by value |
