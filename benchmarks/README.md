# AutoMapSharp Benchmarks

BenchmarkDotNet comparisons between hand-written code, AutoMapSharp, AutoMapper 16.3.0, and Mapster 10.0.14.

## Scenarios

| Benchmark class | What it maps |
|---|---|
| `FlatMappingBenchmark` | One object with 10 primitive properties |
| `NestedMappingBenchmark` | An order with a nested customer, a nested address, and 3 order lines |
| `CollectionMappingBenchmark` | A list of 1,000 flat objects |
| `MapSourceBenchmark` | One object with a custom `MapSource` / `MapFrom` expression |

## Generated code

This project references the source generator, so the flat and nested maps run generated code. Set `AUTOMAPSHARP_DISABLE_GENERATED=1` before running to measure the compiled engine instead. A plain run prints which plan each map uses.

## Running

A plain run checks that every library produces the same output as the hand-written code, so each benchmark compares equal work:

```
dotnet run -c Release -f net8.0 --project benchmarks/AutoMapSharp.Benchmarks
```

A full run passes BenchmarkDotNet arguments after `--run`:

```
dotnet run -c Release -f net8.0 --project benchmarks/AutoMapSharp.Benchmarks -- --run --filter "*"
dotnet run -c Release -f net8.0 --project benchmarks/AutoMapSharp.Benchmarks -- --run --job short --filter "*Flat*"
```

## AutoMapper licensing

AutoMapper 15 and later are commercially licensed. Without a license key, AutoMapper logs a warning and still runs. If you have a key, set it before running:

```
export AUTOMAPPER_LICENSE_KEY="your-key"
```

Check AutoMapper's license terms before publishing comparison results.

## Results

Raw results are committed under `docs/benchmarks/results/`, one folder per run, with the machine details. The summary is in `docs/benchmarks/performance-report.md`.
