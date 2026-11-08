```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean         | Error        | StdDev     | Ratio  | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------- |-------------:|-------------:|-----------:|-------:|--------:|---------:|--------:|-----------:|------------:|
| HandWritten  |     16.82 μs |     6.411 μs |   0.351 μs |   1.00 |    0.03 |  12.4207 |  3.0823 |  101.62 KB |        1.00 |
| AutoMapSharp | 14,002.62 μs | 3,720.875 μs | 203.954 μs | 832.97 |   18.23 | 234.3750 | 46.8750 | 1954.99 KB |       19.24 |
| AutoMapper   |     18.96 μs |     1.957 μs |   0.107 μs |   1.13 |    0.02 |  13.4583 |  2.5330 |  109.96 KB |        1.08 |
| Mapster      |     17.83 μs |     1.156 μs |   0.063 μs |   1.06 |    0.02 |  12.4207 |  2.5330 |  101.62 KB |        1.00 |
