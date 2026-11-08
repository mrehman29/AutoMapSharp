# AutoMapSharp Release & Target Framework Matrix

This document specifies the supported runtime frameworks, target platforms, packaging configurations, and distribution channels for **AutoMapSharp**.

---

## Supported Target Frameworks (TFMs)

| Target Framework | TFM Identifier | Runtime Status | Supported Platforms | Notes |
|---|---|---|---|---|
| **.NET Standard 2.0** | `netstandard2.0` | Broad Compatibility | Windows, Linux, macOS | Enables usage in legacy **.NET Framework 4.6.2 - 4.8.1** and .NET Core 2.0 - 3.1 apps. |
| **.NET 8.0** | `net8.0` | **LTS (Long Term Support)** | Windows, Linux, macOS, iOS, Android, WASM | Optimized modern runtime targets with hardware-accelerated vectors and Native AOT. |
| **.NET 9.0** | `net9.0` | **STS (Standard Term Support)** | Windows, Linux, macOS, iOS, Android, WASM | Latest modern C# 13 features and JIT loop optimizations. |

---

## Published NuGet Packages

Every package is built deterministically, includes Source Link so you can step into the code while debugging, and ships with a README, an icon and the MIT license.

| Package ID | Version | What it is for | Target frameworks | Contents |
|---|---|---|---|---|
| [`AutoMapSharp`](https://www.nuget.org/packages/AutoMapSharp) | `1.0.0-preview.1` | The mapping engine, configuration API, projections and validation. | `netstandard2.0`, `net8.0`, `net9.0` | Assemblies, XML docs, symbols (`.snupkg`), and the source generator and analyzers in `analyzers/dotnet/cs`. |
| [`AutoMapSharp.Extensions.DependencyInjection`](https://www.nuget.org/packages/AutoMapSharp.Extensions.DependencyInjection) | `1.0.0-preview.1` | `AddAutoMapSharp` for `IServiceCollection`, with assembly scanning. | `netstandard2.0`, `net8.0`, `net9.0` | Assemblies, XML docs, symbols. |
| [`AutoMapSharp.Collections`](https://www.nuget.org/packages/AutoMapSharp.Collections) | `1.0.0-preview.1` | Merging collections by key, a replacement for AutoMapper.Collection. | `netstandard2.0`, `net8.0`, `net9.0` | Assemblies, XML docs, symbols. |
| [`AutoMapSharp.MigrationAnalyzer`](https://www.nuget.org/packages/AutoMapSharp.MigrationAnalyzer) | `1.0.0-preview.1` | IDE warnings and quick fixes that rewrite AutoMapper code. | `netstandard2.0` | Analyzer and code fix assembly, plus the migrator library. |
| [`dotnet-automapsharp-migrate`](https://www.nuget.org/packages/dotnet-automapsharp-migrate) | `1.0.0-preview.1` | A .NET tool that applies the migration rules to a whole folder. | `net8.0`, `net9.0` | Tool with the command `automapsharp-migrate`. |

How packages are published is described in [Publishing to NuGet](publishing-to-nuget.md).

---

## Verification & Compatibility Guarantees

1. **No native dependencies.** Every package is managed C# code.
2. **Native AOT.** Maps produced by the source generator run under Native AOT, and CI publishes and runs a real native binary. Maps that need the reflection-based engine are not supported under AOT yet. See [Native AOT & Trimming](../features/native-aot-and-trimming.md).
3. **Packages are tested as packages.** Every CI run and every release packs the `.nupkg` files and runs a separate consumer test project and the sample app against them before anything is published.
4. **License.** MIT, with no commercial tier and no telemetry.
