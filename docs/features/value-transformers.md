# Global & Type-Level Value Transformers

## Overview
Often you want to apply common sanitization, normalization, or formatting across all properties of a specific type (e.g. automatically trimming all string properties, or converting empty strings to null).

AutoMapSharp provides `ValueTransformers` on `MappingModule`, matching AutoMapper's global value transformation capability.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class SanitizedModule : MappingModule
{
    public SanitizedModule()
    {
        // Automatically trim whitespace from every mapped string
        ValueTransformers.Add<string>(val => val != null ? val.Trim() : string.Empty);

        // Convert DateTime to UTC
        ValueTransformers.Add<DateTime>(dt => dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime());

        DefineMap<UserProfileInput, UserProfileDto>();
    }
}
```

```csharp
var dirtyInput = new UserProfileInput
{
    Bio = "   I love open source software!   \n\t",
    Nickname = "   coder99   "
};

var cleanDto = engine.Map<UserProfileInput, UserProfileDto>(dirtyInput);

// Result:
// cleanDto.Bio == "I love open source software!"
// cleanDto.Nickname == "coder99"
```

## Transforms for one member

`AddTransform` changes the final value of one member. It runs after the module-level transformers, in the order added:

```csharp
DefineMap<Invoice, InvoiceDto>()
    .ForProperty(d => d.Note, o => o.AddTransform(n => n.Trim()).AddTransform(n => n + "!"));
```

Module-level transformers are matched to the exact member type. A transformer for `string` also runs for a `null` string, so write it null-safe, for example `s => s?.Trim()!`.

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `ValueTransformers.Add<string>(v => v.Trim())` | `ValueTransformers.Add<string>(v => v.Trim())` | Direct drop-in replacement |
| Profile-level transformers | Module-level transformers | Configured in `MappingModule` constructor |

## Performance Considerations
- Transformers are chained in registration order.
- They execute before destination property assignment, guaranteeing that all downstream consumers receive clean, sanitized data.
