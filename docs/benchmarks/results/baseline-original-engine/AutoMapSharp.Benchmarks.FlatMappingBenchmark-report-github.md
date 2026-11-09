```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  Categories=Flat  

```
| Method       | Mean        | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| HandWritten  |    14.62 ns |  0.324 ns | 0.018 ns |  1.00 |    0.00 | 0.0115 |      96 B |        1.00 |
| AutoMapSharp | 1,000.83 ns | 35.350 ns | 1.938 ns | 68.45 |    0.14 | 0.2308 |    1944 B |       20.25 |
| AutoMapper   |    45.04 ns | 23.281 ns | 1.276 ns |  3.08 |    0.08 | 0.0114 |      96 B |        1.00 |
| Mapster      |    23.26 ns |  3.184 ns | 0.175 ns |  1.59 |    0.01 | 0.0115 |      96 B |        1.00 |
