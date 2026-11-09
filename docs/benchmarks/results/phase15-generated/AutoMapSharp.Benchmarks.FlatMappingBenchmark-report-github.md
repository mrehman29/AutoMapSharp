```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  Categories=Flat  

```
| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| HandWritten  | 15.72 ns | 0.885 ns | 0.049 ns |  1.00 |    0.00 | 0.0115 |      96 B |        1.00 |
| AutoMapSharp | 33.21 ns | 0.460 ns | 0.025 ns |  2.11 |    0.01 | 0.0220 |     184 B |        1.92 |
| AutoMapper   | 46.95 ns | 8.037 ns | 0.441 ns |  2.99 |    0.03 | 0.0114 |      96 B |        1.00 |
| Mapster      | 24.73 ns | 0.666 ns | 0.036 ns |  1.57 |    0.00 | 0.0115 |      96 B |        1.00 |
