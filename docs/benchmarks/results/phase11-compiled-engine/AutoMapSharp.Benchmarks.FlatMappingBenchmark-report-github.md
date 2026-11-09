```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  Categories=Flat  

```
| Method       | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |---------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| HandWritten  | 14.97 ns |  2.726 ns | 0.149 ns |  1.00 |    0.01 | 0.0115 |      96 B |        1.00 |
| AutoMapSharp | 30.24 ns |  5.418 ns | 0.297 ns |  2.02 |    0.02 | 0.0181 |     152 B |        1.58 |
| AutoMapper   | 46.06 ns | 13.538 ns | 0.742 ns |  3.08 |    0.05 | 0.0114 |      96 B |        1.00 |
| Mapster      | 24.88 ns |  2.815 ns | 0.154 ns |  1.66 |    0.02 | 0.0115 |      96 B |        1.00 |
