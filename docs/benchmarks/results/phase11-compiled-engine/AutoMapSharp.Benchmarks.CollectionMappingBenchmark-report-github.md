```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| HandWritten  | 16.46 μs | 0.835 μs | 0.046 μs |  1.00 |    0.00 | 12.4207 | 3.0823 | 101.62 KB |        1.00 |
| AutoMapSharp | 25.28 μs | 2.142 μs | 0.117 μs |  1.54 |    0.01 | 12.4207 | 2.7466 | 101.71 KB |        1.00 |
| AutoMapper   | 19.61 μs | 5.704 μs | 0.313 μs |  1.19 |    0.02 | 13.4583 | 2.5330 | 109.96 KB |        1.08 |
| Mapster      | 17.99 μs | 2.688 μs | 0.147 μs |  1.09 |    0.01 | 12.4207 | 2.5330 | 101.62 KB |        1.00 |
