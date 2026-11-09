# Changelog

All notable changes to AutoMapSharp are listed here. The project follows [Semantic Versioning](https://semver.org/).

## 1.0.0-preview.1

The first public preview of AutoMapSharp, a free and MIT-licensed object mapper for .NET.

### Packages

| Package | What it is for |
|---|---|
| `AutoMapSharp` | The mapping engine, with the source generator and compile-time analyzers built in |
| `AutoMapSharp.Extensions.DependencyInjection` | One-line registration with `Microsoft.Extensions.DependencyInjection` |
| `AutoMapSharp.Collections` | Merging collections by key, a replacement for AutoMapper.Collection |
| `AutoMapSharp.MigrationAnalyzer` | IDE warnings and quick fixes that rewrite AutoMapper code |
| `dotnet-automapsharp-migrate` | A command-line tool that applies the same rewrites to a whole folder |

### Highlights

- **Familiar API.** Mapping modules, `DefineMap`, `ForProperty`, `ForPath`, `ForCtorParam`, `ReverseMap` with unflattening, open generic maps and inline configuration.
- **Customization.** Property resolvers, type converters, value converters, conditions and pre-conditions, fallbacks, transforms, lifecycle callbacks and mapping actions resolved from DI.
- **Inheritance.** `Include`, `IncludeBase`, `IncludeAllDerived` and `As<T>()`.
- **Collections and dictionaries.** Lists, arrays, sets, immutable collections, dictionaries and `ExpandoObject`, with in-place updates of existing collections.
- **Query projection.** `ProjectOnto<T>()` for EF Core, with parameters, explicit expansion, fallbacks as `COALESCE`, inheritance and depth limits.
- **Validation.** `AssertConfigurationIsValid()` checks members, types, constructors and nested maps.
- **Performance.** Every map is compiled once into an expression tree. A source generator writes typed code for eligible maps, which makes the first call faster and lets those maps run under Native AOT.
- **Tooling.** Analyzers AMS001 to AMS003, a migration analyzer with quick fixes, and a migration tool.
- **Targets.** .NET Standard 2.0, .NET 8 and .NET 9.

### Before you upgrade from AutoMapper

Read the [migration guide](MIGRATION_GUIDE.md). It has a side-by-side API table and lists the few places where behavior differs. Check [KNOWN_ISSUES.md](KNOWN_ISSUES.md) and the [parity matrix](docs/automapper-parity-matrix.md) for what is and is not supported yet.
