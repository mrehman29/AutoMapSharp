# Known Issues

A code review on 2026-11-07 found the issues below. Each one has a regression test in `tests/AutoMapSharp.Tests/Regression/`, tagged with its finding ID.

To run the tests for one finding:

```
dotnet test tests/AutoMapSharp.Tests --filter "Finding=F01"
```

Every finding from the review is fixed and has a passing regression test. No known issues are open.

Features that are not supported yet are listed in the [AutoMapper parity matrix](docs/automapper-parity-matrix.md). To report a problem, open an issue at https://github.com/mrehman29/AutoMapSharp/issues.

## Fixed

| ID | Issue | Fixed in |
|---|---|---|
| F01 | Nullable value-type properties were mapped to their default value | Phase 10 |
| F02 | Struct properties such as `TimeSpan` and `DateOnly` were mapped to their default value | Phase 10 |
| F03 | Parameterized `ProjectOnto` reused the parameter values from the first call | Phase 10 |
| F04 | Parameterized `ProjectOnto` replaced entity members with the same name as a parameter | Phase 10 |
| F05 | Projections were cached across engines | Phase 10 |
| F06 | Destination properties named `Type`, `HashCode` or `String` read `GetType()` and similar methods | Phase 10 |
| F07 | `object`-typed properties became an empty `new object()` | Phase 10 |
| F18 | Nested objects of the same type were deep-cloned instead of assigned by reference | Phase 10 |
| F30 | The migration analyzer reported, and the migrator rewrote, unrelated APIs with the same names, such as EF Core's `Ignore()` and Mapster's `IMapper` | Phase 10 |
| F08 | With `PreserveReferences`, one source object mapped to two destination types threw | Phase 11 |
| F09 | Records whose constructor takes nested types or collections threw | Phase 11 |
| F10 | `ReverseMap` of a flattened `MapSource` wrote into the wrong member | Phase 11 |
| F11 | `ForProperty(d => d.A.B, ...)` silently configured the root member `B`. It now throws and points to the new `ForPath` | Phase 11 |
| F12 | Enum to enum mapped by numeric value, not by name | Phase 11 |
| F13 | Many collection destination types, and `Uri`, threw | Phase 11 |
| F14 | Exceptions inside `MapSource` lambdas were swallowed | Phase 11 |
| F15 | Two modules defining the same map raised no error | Phase 11 |
| F16 | `IncludeBase` ran base `BeforeMap` actions in reverse order and was not thread-safe | Phase 11 |
| F17 | Module settings only applied to maps defined after them | Phase 11 |
| F21 | Every `MapSource` expression was compiled again on every map call | Phase 11 |
| F22 | Nested objects threw and caught an exception each | Phase 11 |
| F23 | Flattening used reflection on every call | Phase 11 |
| F19 | The DI engine was a singleton holding the root provider, so resolvers could not use scoped services | Phase 12 |
| F20 | Unregistered resolvers could not have constructor dependencies, and scanning skipped internal modules | Phase 12 |
| F26 | Validation skipped maps that use `ConstructUsing` and did not check member types | Phase 13 |
| F27 | The AMS001 analyzer used its own name rules and disagreed with the runtime | Phase 13 |
| F29 | The migration tool rewrote `AutoMapper.QueryableExtensions` to a namespace without `ProjectOnto` | Phase 13 |
| F28 | Projections ignored `Fallback`, resolvers, converters, conditions, inheritance and `MaxDepth` without any error | Phase 14 |

## Claims withdrawn and restored

Earlier builds claimed compile-time source generation, Native AOT support and benchmark numbers that the code did not back up. Those claims were withdrawn, then delivered with evidence:

* **Source generation:** the generator emits typed code for eligible maps. See [Source Generator](docs/features/source-generator.md).
* **Native AOT:** generated maps run as a native binary, which CI publishes and runs. Maps that are not generated are not supported under Native AOT. See [Native AOT & Trimming](docs/features/native-aot-and-trimming.md).
* **Benchmarks:** every number comes from a committed BenchmarkDotNet run. See the [performance report](docs/benchmarks/performance-report.md).
