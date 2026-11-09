```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| HandWritten  | 11.53 ns | 1.134 ns | 0.062 ns |  1.00 |    0.01 | 0.0096 |      80 B |        1.00 |
| AutoMapSharp | 32.80 ns | 9.108 ns | 0.499 ns |  2.85 |    0.04 | 0.0239 |     200 B |        2.50 |
| AutoMapper   | 45.62 ns | 0.794 ns | 0.044 ns |  3.96 |    0.02 | 0.0134 |     112 B |        1.40 |
| Mapster      | 22.67 ns | 0.270 ns | 0.015 ns |  1.97 |    0.01 | 0.0134 |     112 B |        1.40 |
