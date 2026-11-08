# Source Generator

The AutoMapSharp package includes a Roslyn source generator. At build time it emits typed mapping code for the maps whose behavior it can reproduce exactly. Those maps start faster and run under Native AOT. Every other map runs on the engine's compiled expression trees, as before.

You don't configure anything. The generator runs whenever a project references the `AutoMapSharp` package.

## Which maps are generated

A map defined with `DefineMap<TSource, TDestination>()` is generated when:

* The destination is a class with a public parameterless constructor, and its members have public setters that are not `init`.
* Every member is matched by name, naming convention, prefixes and suffixes, member name replacements or flattening, or is configured with `Skip()` or `UseValue(...)`.
* Each value is assigned as is, or converted with a built-in conversion: nullable values, enums, numbers, strings, `Guid`, dates, nested objects through their own maps, and `List<T>`, `T[]` or list interfaces.
* The chain only uses `ForProperty` with `Skip` or `UseValue`, `MaxDepth`, `PreserveReferences`, `AllowNullCollections`, `ValidateMembers`, `ForSourceMember`, `Include`, `IncludeBase` and `IncludeAllDerived`.
* The module does not use value transformers, `ShouldMap` hooks or `IncludeSourceExtensionMethods`.

Anything else, such as `MapSource`, resolvers, converters, conditions, callbacks, `ForPath` or constructor mapping, keeps the map on the compiled engine. The build reports an `AMS100` message for each such map with the reason. It is an info message, so it never fails a build.

## How generated code stays correct

Each generated map records the assumptions it was built on: which source member feeds each destination member, which members are skipped or constant, which type pairs it assigns or converts itself, and which element maps its collections use.

The first time the engine maps that pair, it checks every assumption against the frozen runtime configuration. If anything differs, for example a module setting the generator could not see, or another engine that defines an extra map, the engine uses its compiled plan instead. Generated code therefore never behaves differently from the compiled engine.

The test suite proves this. It runs with the generator attached, so every eligible test map uses generated code, and CI runs it again with generated code disabled.

## Seeing what runs

```csharp
MapPlanInfo info = engine.GetPlanInfo(typeof(Order), typeof(OrderDto));
Console.WriteLine(info); // "Generated", or "Compiled (the source generator did not emit code for this pair)"

foreach (var (source, destination, plan) in engine.DescribePlans())
{
    Console.WriteLine($"{source.Name} -> {destination.Name}: {plan}");
}
```

Set the environment variable `AUTOMAPSHARP_DISABLE_GENERATED=1` to ignore generated code, for example to compare behavior or performance.

## Performance

| Measure | Compiled plan | Generated code |
|---|---|---|
| First map of a nested order in a new process | about 29.5 ms | about 16.5 ms |
| Steady-state flat map | 30.2 ns | 33.2 ns |
| Steady-state nested order | 154.2 ns | 163.4 ns |

Generated code mainly helps start-up, because no expression tree is built or compiled. In steady state both run at the same speed, within benchmark noise, because compiled expression trees are already native code. See the [performance report](../benchmarks/performance-report.md).

## Native AOT

Generated maps run under Native AOT. See [Native AOT & Trimming](native-aot-and-trimming.md).

## AutoMapper Migration Guide

AutoMapper has no source generator. Nothing changes in your mapping code. Maps that are not generated work exactly as before.
