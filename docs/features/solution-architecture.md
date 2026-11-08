# Solution Architecture & Multi-Targeting Strategy

AutoMapSharp is a cleanroom mapping library for modern and legacy .NET.

> **Status:** The source generator emits typed code for eligible maps. Every other map runs on the runtime engine, which compiles one expression-tree plan per type pair. The engine checks generated code against the runtime configuration before using it.

---

## 1. Architectural Tiers

```
┌────────────────────────────────────────────────────────┐
│        Compile-Time Roslyn Generator                    │
│        (AutoMapSharp.Generators - netstandard2.0)       │
│                                                        │
│  • Direct field-to-field C# assignment generation      │
│  • Typed code for maps it can reproduce exactly        │
│  • Runs under Native AOT, checked at run time          │
└────────────────────────────────────────────────────────┘
                            ▲
                            │ Fallback for Dynamic/Untyped Mappings
                            ▼
┌────────────────────────────────────────────────────────┐
│      Runtime Engine (compiled expression plans)         │
│                 (AutoMapSharp Core)                     │
│                                                        │
│  • One compiled plan per type pair, built on first use │
│  • Deep flattening & naming convention resolution      │
│  • LINQ Expression Tree builder for ORM projections    │
└────────────────────────────────────────────────────────┘
                            ▲
                            │
┌───────────────────────────┴────────────────────────────┐
│                  Roslyn Analyzers & CLI                │
│    (AutoMapSharp.Analyzers & AutoMapSharp.Cli)          │
│                                                        │
│  • Compile-time diagnostics (AMS001, AMS002, AMS003)   │
│  • Lightbulb quick fixes and CLI automated migrations  │
└────────────────────────────────────────────────────────┘
```

---

## 2. Inside the Runtime Engine

| Part | Responsibility |
|---|---|
| `MappingConfiguration` | Freezes the modules into effective maps when the engine is built: module settings, `IncludeBase` merging, duplicate detection and reverse-map unflattening. |
| `MapperHolder<TSource, TDestination>` | One per type pair. Runs polymorphic dispatch, type converters, reference preservation, `MaxDepth` and error wrapping around the compiled plan. |
| `PlanBuilder` | Builds the expression tree for one type pair: construction, members, `ForPath` rules and callbacks. |
| `SourceMemberMatcher` | Finds the source member for a destination member. The engine, validation and reverse-map unflattening all use it. |
| `BuiltInConversions`, `CollectionPlanner` | Choose value and collection conversions when the plan is built. |

Plans call the holders of nested type pairs directly, so a nested map costs one method call. Set `AUTOMAPSHARP_PREFER_INTERPRETATION=1` to run plans through the expression interpreter, which is what platforms without dynamic code do.

---

## 3. Solution Structure

* **`AutoMapSharp`**: Core library multi-targeting `netstandard2.0`, `net8.0`, and `net9.0`. Contains mapping interfaces, fluent builder, engine, expression compilers, and LINQ projection extensions.
* **`AutoMapSharp.Generators`**: Roslyn incremental source generator. It emits typed maps with their assumptions and registers them from a module initializer. See [Source Generator](source-generator.md).
* **`AutoMapSharp.Analyzers`**: Roslyn diagnostic analyzers and code fix providers enforcing compile-time mapping safety.
* **`AutoMapSharp.MigrationAnalyzer`**: Specialized analyzer detecting legacy AutoMapper syntax (`AMSMIG001` - `AMSMIG008`) and providing one-click migration.
* **`AutoMapSharp.Cli`**: Global .NET CLI tool (`dotnet-automapsharp-migrate`) for batch repository migrations.
* **`AutoMapSharp.Extensions.DependencyInjection`**: Dependency injection integration for `IServiceCollection` (`AddAutoMapSharp`).
* **`AutoMapSharp.Sample.Web`**: Interactive ASP.NET Core MVC web application with side-by-side Razor views demonstrating every feature with hardcoded mock database data and EF Core SQLite.
* **`AutoMapSharp.Tests`**: Comprehensive xUnit test suite (46 unit tests across `net8.0` and `net9.0`).
* **`AutoMapSharp.IntegrationTests.NugetConsumer`**: End-to-end integration test suite consuming locally packed `.nupkg` packages (44 tests across `net8.0` and `net9.0`).
* **`AutoMapSharp.Tests.NativeAot`**: Native AOT standalone executable verification testing trimming with zero warnings.
* **`AutoMapSharp.Benchmarks`**: BenchmarkDotNet suite comparing throughput and memory allocations against AutoMapper, Mapster, and hand-written C#.

---

## 4. Multi-Targeting Strategy

| Framework | Purpose |
|---|---|
| **`netstandard2.0`** | Broad compatibility with .NET Framework 4.6.1 - 4.8.1 and legacy .NET Core 2.0 - 3.1 applications. |
| **`net8.0`** | Modern Long Term Support (LTS) cloud-native target with hardware intrinsics and Native AOT. |
| **`net9.0`** | Standard Term Support (STS) with modern C# 13 language features and JIT loop enhancements. |
