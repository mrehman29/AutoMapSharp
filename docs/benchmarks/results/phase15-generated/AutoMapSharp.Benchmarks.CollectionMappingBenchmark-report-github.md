```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean     | Error    | StdDev   | Ratio | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| HandWritten  | 17.60 μs | 2.515 μs | 0.138 μs |  1.00 | 12.4207 | 3.0823 | 101.62 KB |        1.00 |
| AutoMapSharp | 27.78 μs | 2.008 μs | 0.110 μs |  1.58 | 12.4512 | 2.9602 | 101.74 KB |        1.00 |
| AutoMapper   | 20.49 μs | 0.936 μs | 0.051 μs |  1.16 | 13.4583 | 2.5330 | 109.96 KB |        1.08 |
| Mapster      | 18.61 μs | 1.953 μs | 0.107 μs |  1.06 | 12.4207 | 2.5330 | 101.62 KB |        1.00 |
