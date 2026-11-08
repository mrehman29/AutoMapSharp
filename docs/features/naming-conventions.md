# Naming Conventions & Member Filtering

## Overview
AutoMapSharp provides first-class support for cross-casing conventions (such as `snake_case` to `PascalCase`) and member name prefix/suffix stripping.

## AutoMapSharp Usage

### 1. Cross-Casing Conventions
```csharp
using AutoMapSharp;
using AutoMapSharp.Conventions;

public class SnakeToPascalModule : MappingModule
{
    public SnakeToPascalModule()
    {
        // Configure source properties as snake_case and destination as PascalCase
        SourceNamingConvention = SnakeCaseNamingConvention.Instance;
        DestinationNamingConvention = PascalCaseNamingConvention.Instance;

        // Maps: first_name -> FirstName, order_date -> OrderDate
        DefineMap<SnakeUser, PascalUserDto>();
    }
}
```

### 2. Prefix & Suffix Stripping
Strip private prefixes (e.g. `m_`, `_`) and type suffixes (e.g. `Model`, `Dto`) during matching:
```csharp
public class CleanNamingModule : MappingModule
{
    public CleanNamingModule()
    {
        RecognizePrefixes("m_", "_", "Get");
        RecognizeSuffixes("Model", "Dto");

        // Maps: m_Title -> Title, CategoryModel -> Category
        DefineMap<SourceEntity, CleanDto>();
    }
}
```

## More convention settings

```csharp
public class LegacyModule : MappingModule
{
    public LegacyModule()
    {
        ClearPrefixes();                    // Also removes the default "Get".
        RecognizeDestinationPrefixes("Dto"); // DtoName matches Name.
        RecognizeDestinationSuffixes("Field");
        ReplaceMemberName("Ä", "A");         // Applied to source member names before matching.
        DefineMap<LegacyRow, CleanRow>();
    }
}
```

camelCase source members, such as `firstName`, match PascalCase destinations, such as `FirstName`, without configuration.

The Roslyn analyzer reads `RecognizePrefixes`, `RecognizeSuffixes`, `ClearPrefixes` and a snake_case `SourceNamingConvention` from the module, so it agrees with the engine. It can only read string literal arguments.

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax |
|---|---|
| `cfg.SourceMemberNamingConvention = new LowerUnderscoreNamingConvention();` | `SourceNamingConvention = SnakeCaseNamingConvention.Instance;` |
| `cfg.DestinationMemberNamingConvention = new PascalCaseNamingConvention();` | `DestinationNamingConvention = PascalCaseNamingConvention.Instance;` |
| `cfg.RecognizePrefixes("m_", "Get");` | `RecognizePrefixes("m_", "Get");` |
| `cfg.RecognizePostfixes("Model");` | `RecognizeSuffixes("Model");` |
