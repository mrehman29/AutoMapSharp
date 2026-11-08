using System;
using System.Linq;
using System.Text.Json;
using BenchmarkDotNet.Running;
using Mapster;

namespace AutoMapSharp.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--run")
        {
            // Example: dotnet run -c Release -f net8.0 -- --run --job short --filter "*"
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Skip(1).ToArray());
            return 0;
        }

        Console.WriteLine("AutoMapSharp benchmarks compiled. Run with '--run' and BenchmarkDotNet arguments to execute them.");
        foreach (var (source, destination) in new[] { (typeof(FlatSource), typeof(FlatDestination)), (typeof(Order), typeof(OrderDto)), (typeof(PersonSource), typeof(PersonDestination)) })
        {
            Console.WriteLine($"[Plan] {source.Name} -> {destination.Name}: {((MapEngine)Mappers.AutoMapSharp).GetPlanInfo(source, destination)}");
        }
        return SmokeTest() ? 0 : 1;
    }

    /// <summary>
    /// Checks that every library produces the same output as the hand-written baseline,
    /// so the benchmarks compare equal work.
    /// </summary>
    private static bool SmokeTest()
    {
        bool ok = true;

        void Check<T>(string scenario, T expected, Func<T> autoMapSharp, Func<T> autoMapper, Func<T> mapster)
        {
            var want = JsonSerializer.Serialize(expected);
            foreach (var (name, run) in new[] { ("AutoMapSharp", autoMapSharp), ("AutoMapper", autoMapper), ("Mapster", mapster) })
            {
                var got = JsonSerializer.Serialize(run());
                var match = got == want;
                ok &= match;
                Console.WriteLine($"[Smoke] {scenario,-10} {name,-12} {(match ? "matches" : "DIFFERS: " + got)}");
            }
        }

        var flat = new FlatSource();
        Check("Flat", new FlatMappingBenchmark().HandWritten(),
            () => Mappers.AutoMapSharp.Map<FlatSource, FlatDestination>(flat),
            () => Mappers.AutoMapper.Map<FlatSource, FlatDestination>(flat),
            () => flat.Adapt<FlatSource, FlatDestination>(Mappers.Mapster));

        var order = new Order();
        Check("Nested", HandWrittenMaps.Map(order),
            () => Mappers.AutoMapSharp.Map<Order, OrderDto>(order),
            () => Mappers.AutoMapper.Map<Order, OrderDto>(order),
            () => order.Adapt<Order, OrderDto>(Mappers.Mapster));

        var person = new PersonSource();
        Check("MapSource", new MapSourceBenchmark().HandWritten(),
            () => Mappers.AutoMapSharp.Map<PersonSource, PersonDestination>(person),
            () => Mappers.AutoMapper.Map<PersonSource, PersonDestination>(person),
            () => person.Adapt<PersonSource, PersonDestination>(Mappers.Mapster));

        return ok;
    }
}
