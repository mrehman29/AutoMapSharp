# AutoMapSharp

[![NuGet](https://img.shields.io/nuget/vpre/AutoMapSharp.svg)](https://www.nuget.org/packages/AutoMapSharp)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net8.0%20%7C%20net9.0-512BD4)](https://dotnet.microsoft.com/)

A high-performance, enterprise-grade, open-source object-to-object mapping library for .NET.

`AutoMapSharp` is a cleanroom open-source alternative to AutoMapper, built from the ground up to provide familiar mapping idioms under the MIT license.

> **Preview status:** AutoMapSharp is in preview. Every issue found in a code review is fixed and covered by a regression test, see [KNOWN_ISSUES.md](KNOWN_ISSUES.md). The [parity matrix](docs/automapper-parity-matrix.md) lists AutoMapper features and their status. A source generator emits typed code for eligible maps, and those maps run under Native AOT, which CI verifies with a real native binary.

---

## Key Features

* **Compiled Mapping Engine:** Convention-based mapping configured through `MappingModule` classes. Each type pair is compiled once into an expression-tree plan. In benchmarks it is faster than AutoMapper 16.3.0 for flat objects and within 1.4 times of it for nested objects and lists. A source generator emits typed code for maps it can reproduce exactly, for faster start-up and [Native AOT](docs/features/native-aot-and-trimming.md).
* **Multi-Targeting:** Multi-targets `netstandard2.0`, `net8.0`, and `net9.0` for full compatibility with legacy .NET Framework applications and modern cloud-native apps.
* **Modern C# 9-13 Ready:** Built-in support for positional records, `init`-only properties, `required` members, and Nullable Reference Types (`#nullable enable`).
* **ORM Query Projection:** First-class `ProjectOnto<T>()` LINQ expression translator for Entity Framework Core.
* **Enterprise Tested:** Unmapped property validation (`AssertConfigurationIsValid`), circular reference protection (`MaxDepth`, `PreserveReferences`), and bidirectional mapping (`ReverseMap`).

---

## Quick Start

### 1. Install via NuGet
```bash
dotnet add package AutoMapSharp
dotnet add package AutoMapSharp.Extensions.DependencyInjection
# Optional: merge collections by key, like AutoMapper.Collection
dotnet add package AutoMapSharp.Collections
```

| Package | What it is for |
|---|---|
| [AutoMapSharp](https://www.nuget.org/packages/AutoMapSharp) | The mapping engine, with the source generator and analyzers built in |
| [AutoMapSharp.Extensions.DependencyInjection](https://www.nuget.org/packages/AutoMapSharp.Extensions.DependencyInjection) | `AddAutoMapSharp` for `IServiceCollection` |
| [AutoMapSharp.Collections](https://www.nuget.org/packages/AutoMapSharp.Collections) | Merging collections by key |
| [AutoMapSharp.MigrationAnalyzer](https://www.nuget.org/packages/AutoMapSharp.MigrationAnalyzer) | IDE quick fixes that rewrite AutoMapper code |
| [dotnet-automapsharp-migrate](https://www.nuget.org/packages/dotnet-automapsharp-migrate) | A .NET tool that migrates a whole folder from AutoMapper |

While AutoMapSharp is in preview, add `--prerelease` to `dotnet add package`, or tick "Include prerelease" in your IDE.

### 2. Define a Mapping Module
```csharp
using AutoMapSharp;

public class UserMappingModule : MappingModule
{
    public UserMappingModule()
    {
        DefineMap<User, UserDto>()
            .ForProperty(dest => dest.FullName, opt => opt.MapSource(src => src.FirstName + " " + src.LastName))
            .ForProperty(dest => dest.PasswordHash, opt => opt.Skip())
            .ReverseMap();
    }
}
```

### 3. Register in Dependency Injection (`Program.cs`)
```csharp
using AutoMapSharp.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Auto-discovers all MappingModule types in the assembly
builder.Services.AddAutoMapSharp(typeof(Program).Assembly);

var app = builder.Build();
```

### 4. Inject and Map
```csharp
public class UserService
{
    private readonly IMapEngine _mapper;

    public UserService(IMapEngine mapper)
    {
        _mapper = mapper;
    }

    public UserDto CreateDto(User user)
    {
        return _mapper.Map<User, UserDto>(user);
    }

    public void UpdateEntity(UpdateUserDto dto, User entity)
    {
        // In-place update preserving EF Core change tracking
        _mapper.Map(dto, entity);
    }
}
```

---

## Migrating from AutoMapper

Check out our comprehensive [AutoMapper Migration Guide](MIGRATION_GUIDE.md) for a side-by-side syntax comparison table and upgrade steps.

To do most of the rewriting automatically, add the migration analyzer to your project and apply its quick fixes, or run the migration tool over your source folder:

```bash
dotnet add package AutoMapSharp.MigrationAnalyzer --prerelease
# or
dotnet tool install --global dotnet-automapsharp-migrate --prerelease
automapsharp-migrate ./src --dry-run
```

---

## Documentation & Samples
* [Solution Architecture & Multi-Targeting Strategy](docs/features/solution-architecture.md)
* [Interactive Web Application Showcase (with Views)](samples/AutoMapSharp.Sample.Web/README.md)
* [Core Engine Interface (`IMapEngine`)](docs/features/core-engine-interface.md)
* [Mapping Modules (`MappingModule`)](docs/features/mapping-modules.md)
* [Dependency Injection Setup](docs/features/dependency-injection.md)
* [Property Matching Conventions](docs/features/property-matching-conventions.md)
* [Deep Object Flattening](docs/features/object-flattening.md)
* [Naming Conventions & Member Filtering](docs/features/naming-conventions.md)
* [Built-in Type Conversions](docs/features/type-conversions.md)
* [Modern C# Features Support (Records, Init, NRT)](docs/features/modern-csharp-features.md)
* [Fluent Configuration API Overview](docs/features/fluent-configuration-overview.md)
* [Property Skipping & Constant Values (`Skip`, `UseValue`)](docs/features/property-skipping-and-constant-values.md)
* [Custom Source Property Redirection (`MapSource`)](docs/features/source-property-redirection.md)
* [Conditionals & Fallbacks (`If`, `Fallback`)](docs/features/conditional-mapping-and-fallbacks.md)
* [Bidirectional Mapping (`ReverseMap`)](docs/features/bidirectional-reverse-mapping.md)
* [Global & Type-Level Value Transformers](docs/features/value-transformers.md)
* [Collections Mapping (`IEnumerable`, `List`, Arrays)](docs/features/collections-mapping.md)
* [Sets & Dictionaries Mapping](docs/features/sets-and-dictionaries.md)
* [Null-Collection Handling Policies](docs/features/null-collection-handling.md)
* [Existing Collection In-Place Updates](docs/features/existing-collection-updates.md)
* [Nested Complex Object Mapping & Sub-Mapper Chaining](docs/features/nested-object-mapping.md)
* [Constructor Mapping & Immutable Factory Instantiation (`ConstructUsing`)](docs/features/constructor-mapping-and-records.md)
* [Circular References & Recursion Depth Limits](docs/features/circular-references-and-depth-limits.md)
* [Custom Property Resolvers (`IPropertyResolver`)](docs/features/custom-property-resolvers.md)
* [Type-Level Converters (`ITypeConverter` & `ConvertUsing`)](docs/features/type-level-converters.md)
* [Subclass & Polymorphic Mapping (`Include` & `IncludeBase`)](docs/features/polymorphic-subclass-mapping.md)
* [Mapping Lifecycle Callbacks (`BeforeMap` & `AfterMap`)](docs/features/lifecycle-callbacks.md)
* [Configuration Validation (`AssertConfigurationIsValid`)](docs/features/configuration-validation.md)
* [ORM Database Projections (`ProjectOnto`)](docs/features/orm-projection-overview.md)
* [EF Core Expression Tree Translation](docs/features/efcore-expression-trees.md)
* [Parameterized Database Projections](docs/features/parameterized-projections.md)
* [Explicit Expansion for Navigation Properties](docs/features/explicit-expansion.md)
* [Roslyn Analyzers & Compile-Time Diagnostics (`AMS001` - `AMS003`)](docs/features/roslyn-analyzers-and-diagnostics.md)
* [First-Party Roslyn IDE Code Fix Providers](docs/features/ide-code-fixes.md)
* [Installing and Configuring the Migration Analyzer](docs/migration/analyzer-setup.md)
* [Complete AutoMapper to AutoMapSharp Migration Guide](MIGRATION_GUIDE.md)
* [BenchmarkDotNet Performance & Memory Report](docs/benchmarks/performance-report.md)
* [AutoMapper Parity Matrix](docs/automapper-parity-matrix.md)
* [Inline Configuration](docs/features/inline-configuration.md)
* [Open Generic Maps](docs/features/open-generic-maps.md)
* [Value Converters](docs/features/value-converters.md)
* [Per-Call Options](docs/features/per-call-options.md)
* [Dictionaries & ExpandoObject](docs/features/dictionary-mapping.md)
* [Source Extension Methods](docs/features/source-extension-methods.md)
* [Attribute Configuration](docs/features/attribute-mapping.md)
* [Execution Plans](docs/features/execution-plans.md)
* [Collection Merging (AutoMapSharp.Collections)](docs/features/collection-merging.md)
* [ForPath & ReverseMap Unflattening](docs/features/forpath-and-unflattening.md)
* [Enum Mapping](docs/features/enum-mapping.md)
* [Source Generator](docs/features/source-generator.md)
* [Native AOT & Trimming](docs/features/native-aot-and-trimming.md)
* [Release & Target Framework Matrix](docs/deployment/release-matrix.md)
* [Publishing to NuGet](docs/deployment/publishing-to-nuget.md)
* [Changelog](CHANGELOG.md)

---

## License
AutoMapSharp is licensed under the [MIT License](LICENSE).
