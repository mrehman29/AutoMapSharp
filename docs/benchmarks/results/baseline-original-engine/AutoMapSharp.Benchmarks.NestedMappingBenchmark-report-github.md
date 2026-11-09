```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean          | Error         | StdDev     | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |--------------:|--------------:|-----------:|---------:|--------:|-------:|----------:|------------:|
| HandWritten  |      91.23 ns |      6.286 ns |   0.345 ns |     1.00 |    0.00 | 0.0688 |     576 B |        1.00 |
| AutoMapSharp | 102,495.87 ns | 11,053.214 ns | 605.864 ns | 1,123.47 |    6.82 | 0.7324 |    6930 B |       12.03 |
| AutoMapper   |     108.75 ns |     24.951 ns |   1.368 ns |     1.19 |    0.01 | 0.0612 |     512 B |        0.89 |
| Mapster      |      89.53 ns |      6.663 ns |   0.365 ns |     0.98 |    0.00 | 0.0602 |     504 B |        0.88 |
