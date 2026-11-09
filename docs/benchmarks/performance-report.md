# AutoMapSharp Performance Report

> **Status:** The numbers previously published on this page did not come from a real benchmark run of this code, and they have been withdrawn. Every number below comes from a real BenchmarkDotNet run, and the raw output is committed next to this page.

## Generated code

The benchmark project references the source generator, so its flat and nested maps run generated code. The `MapSource` map stays compiled. Raw output is in [`results/phase15-generated/`](results/phase15-generated/).

| Scenario | Compiled engine | Generated code | AutoMapper 16.3.0 |
|---|---|---|---|
| Flat object | 30.2 ns | 33.2 ns | 47.0 ns |
| Nested order | 154.2 ns | 163.4 ns | 116.0 ns |
| List of 1,000 | 25.3 µs | 27.8 µs | 20.5 µs |

In steady state, generated code runs at the same speed as compiled plans, within the noise of a short run, because compiled expression trees are already native code. Its benefit is start-up: no expression tree is built or compiled.

| Cold start, new process, first map of a nested order | Time |
|---|---|
| Compiled plan | about 29.5 ms |
| Generated code | about 16.5 ms |

The cold-start figures come from three runs of a small console app on the same machine, timing the first `Map` call after the engine is built.

## Current: compiled engine

| Setting | Value |
|---|---|
| Date | 2026-11-07 |
| Machine | Apple M3 Ultra, macOS 26.5.2 |
| Runtime | .NET 8.0.14, Arm64 RyuJIT |
| Tool | BenchmarkDotNet 0.14.0, `ShortRun` job with 3 iterations |
| Raw output | [`results/phase11-compiled-engine/`](results/phase11-compiled-engine/) |

### Mean time per call

| Scenario | Hand-written | AutoMapSharp | AutoMapper 16.3.0 | Mapster 10.0.14 |
|---|---|---|---|---|
| Flat object, 10 properties | 15.0 ns | 30.2 ns | 46.1 ns | 24.9 ns |
| Nested order with customer, address and 3 lines | 93.5 ns | 154.2 ns | 112.2 ns | 90.9 ns |
| List of 1,000 flat objects | 16.5 µs | 25.3 µs | 19.6 µs | 18.0 µs |
| One `MapSource` / `MapFrom` expression | 10.9 ns | 29.5 ns | 43.6 ns | 21.4 ns |

### Against AutoMapper 16.3.0

| Scenario | AutoMapSharp compared with AutoMapper | Phase 11 target: within 1.5 times |
|---|---|---|
| Flat object | 0.66 times, faster | Met |
| Nested order | 1.37 times | Met |
| List of 1,000 | 1.29 times | Met |
| `MapSource` | 0.68 times, faster | Met |

AutoMapSharp allocates 56 more bytes per call than hand-written code for a flat map. That is the per-call `ResolutionContext`.

### Before and after

| Scenario | Original engine | Compiled engine | Speed-up |
|---|---|---|---|
| Flat object | 1,000.8 ns | 30.2 ns | about 33 times |
| Nested order | 102,495.9 ns | 154.2 ns | about 660 times |
| List of 1,000 | 14,002.6 µs | 25.3 µs | about 550 times |
| `MapSource` | 94,400.6 ns | 29.5 ns | about 3,200 times |

CI runs `scripts/check-benchmarks.sh`, which fails when AutoMapSharp is more than 4 times slower than hand-written code in any scenario of the same run.

## Baseline: original engine against AutoMapper 16.3.0 and Mapster 10.0.14

This is the "before" measurement, taken with the old reflection-based engine.

| Setting | Value |
|---|---|
| Date | 2026-11-07 |
| Machine | Apple M3 Ultra, macOS 26.5.2 |
| Runtime | .NET 8.0.14, Arm64 RyuJIT |
| Tool | BenchmarkDotNet 0.14.0, `ShortRun` job with 3 iterations |
| Raw output | [`results/baseline-original-engine/`](results/baseline-original-engine/) |

The short job gives rough numbers. Treat differences under about 10% as noise.

### Mean time per call

| Scenario | Hand-written | AutoMapSharp | AutoMapper 16.3.0 | Mapster 10.0.14 |
|---|---|---|---|---|
| Flat object, 10 properties | 14.6 ns | 1,000.8 ns | 45.0 ns | 23.3 ns |
| Nested order with customer, address and 3 lines | 91.2 ns | 102,495.9 ns | 108.8 ns | 89.5 ns |
| List of 1,000 flat objects | 16.8 µs | 14,002.6 µs | 19.0 µs | 17.8 µs |
| One `MapSource` / `MapFrom` expression | 10.7 ns | 94,400.6 ns | 42.4 ns | 21.5 ns |

### How much slower the original engine was

| Scenario | Compared with AutoMapper | Memory allocated compared with hand-written |
|---|---|---|
| Flat object | about 22 times slower | 20 times more |
| Nested order | about 940 times slower | 12 times more |
| List of 1,000 | about 740 times slower | 19 times more |
| `MapSource` | about 2,200 times slower | 67 times more |

### Why

* Every map uses reflection to read and write each property.
* Every `MapSource` expression is compiled again on every call. This is finding F21.
* Every nested object first tries a type conversion that throws and catches an exception. BenchmarkDotNet counted 1,000 exceptions per call in the list benchmark. This is finding F22.
* Flattening paths are looked up with reflection on every call. This is finding F23.

Each finding is described in [KNOWN_ISSUES.md](../../KNOWN_ISSUES.md). See also the [benchmarks README](../../benchmarks/README.md).
