# Type-Level Converters (`ITypeConverter` & `ConvertUsing`)

Type-level converters allow developers to completely take over the transformation of one type to another, bypassing member-by-member property reflection and generation. This is ideal for parsing string payloads (e.g. JSON/XML), domain primitive wrappers, or custom binary types.

---

## 1. Class-Based Type Converters

Implement `ITypeConverter<TSource, TDestination>`:

```csharp
using System.Text.Json;
using AutoMapSharp;

public class JsonMetadataConverter : ITypeConverter<string, JsonMetadataPayload>
{
    public JsonMetadataPayload Convert(string source, ResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return new JsonMetadataPayload();
        }

        return JsonSerializer.Deserialize<JsonMetadataPayload>(source) 
               ?? new JsonMetadataPayload();
    }
}
```

Register the converter on the mapping definition:

```csharp
DefineMap<string, JsonMetadataPayload>()
    .ConvertUsing<JsonMetadataConverter>();
```

---

## 2. Inline Lambda Converters

For lightweight transformations, define an inline function directly:

```csharp
// Simple lambda converter
DefineMap<string, int>()
    .ConvertUsing(str => int.TryParse(str, out var v) ? v : 0);

// Lambda converter with ResolutionContext access
DefineMap<DateTime, string>()
    .ConvertUsing((date, ctx) => date.ToString("yyyy-MM-ddTHH:mm:ssZ"));
```

---

## 3. AutoMapper Migration Guide

| AutoMapper Concept | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `ITypeConverter<TSource, TDestination>` | `ITypeConverter<TSource, TDestination>` | Identical interface signature |
| `CreateMap<Src, Dest>().ConvertUsing<MyConverter>()` | `DefineMap<Src, Dest>().ConvertUsing<MyConverter>()` | Completely takes over type mapping |
| `CreateMap<Src, Dest>().ConvertUsing(src => ...)` | `DefineMap<Src, Dest>().ConvertUsing(src => ...)` | Supported for concise lambdas |
| Nested member invocation | Nested member invocation | Automatically invoked when type appears anywhere in graph |
