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
| HandWritten  | 10.86 ns | 0.980 ns | 0.054 ns |  1.00 |    0.01 | 0.0096 |      80 B |        1.00 |
| AutoMapSharp | 29.48 ns | 0.935 ns | 0.051 ns |  2.71 |    0.01 | 0.0201 |     168 B |        2.10 |
| AutoMapper   | 43.56 ns | 2.500 ns | 0.137 ns |  4.01 |    0.02 | 0.0134 |     112 B |        1.40 |
| Mapster      | 21.38 ns | 0.558 ns | 0.031 ns |  1.97 |    0.01 | 0.0134 |     112 B |        1.40 |
