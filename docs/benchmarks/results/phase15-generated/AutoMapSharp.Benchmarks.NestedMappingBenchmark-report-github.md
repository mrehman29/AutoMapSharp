```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Ultra, 1 CPU, 28 logical and 28 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 8.0.14 (8.0.1425.11118), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean      | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|---------:|------:|-------:|----------:|------------:|
| HandWritten  | 101.79 ns |  3.205 ns | 0.176 ns |  1.00 | 0.0688 |     576 B |        1.00 |
| AutoMapSharp | 163.44 ns | 23.878 ns | 1.309 ns |  1.61 | 0.0677 |     568 B |        0.99 |
| AutoMapper   | 116.03 ns |  4.917 ns | 0.270 ns |  1.14 | 0.0612 |     512 B |        0.89 |
| Mapster      |  98.03 ns |  9.228 ns | 0.506 ns |  0.96 | 0.0602 |     504 B |        0.88 |
