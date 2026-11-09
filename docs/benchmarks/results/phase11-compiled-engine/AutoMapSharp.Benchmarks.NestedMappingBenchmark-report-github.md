```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| HandWritten  |  93.46 ns |  1.680 ns | 0.092 ns |  1.00 |    0.00 | 0.0688 |     576 B |        1.00 |
| AutoMapSharp | 154.23 ns | 67.289 ns | 3.688 ns |  1.65 |    0.03 | 0.0639 |     536 B |        0.93 |
| AutoMapper   | 112.21 ns | 24.405 ns | 1.338 ns |  1.20 |    0.01 | 0.0612 |     512 B |        0.89 |
| Mapster      |  90.93 ns | 17.745 ns | 0.973 ns |  0.97 |    0.01 | 0.0602 |     504 B |        0.88 |
