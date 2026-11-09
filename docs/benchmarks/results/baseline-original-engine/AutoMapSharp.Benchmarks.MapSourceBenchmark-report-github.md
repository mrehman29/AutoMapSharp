```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean         | Error         | StdDev     | Ratio    | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------- |-------------:|--------------:|-----------:|---------:|--------:|-------:|-------:|----------:|------------:|
| HandWritten  |     10.74 ns |      0.647 ns |   0.035 ns |     1.00 |    0.00 | 0.0096 |      - |      80 B |        1.00 |
| AutoMapSharp | 94,400.56 ns | 13,394.020 ns | 734.171 ns | 8,788.84 |   64.30 | 0.6104 | 0.2441 |    5351 B |       66.89 |
| AutoMapper   |     42.43 ns |      4.687 ns |   0.257 ns |     3.95 |    0.02 | 0.0134 |      - |     112 B |        1.40 |
| Mapster      |     21.48 ns |      6.438 ns |   0.353 ns |     2.00 |    0.03 | 0.0134 |      - |     112 B |        1.40 |
