using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class BenchmarksController : Controller
{
    private readonly IMapEngine _engine;

    public BenchmarksController(IMapEngine engine)
    {
        _engine = engine;
    }

    public IActionResult Results()
    {
        var model = new BenchmarkStatusViewModel
        {
            Status = "The benchmark numbers previously shown on this page did not come from a real run of this code and have been withdrawn. The numbers below come from real BenchmarkDotNet runs.",
            Method = "BenchmarkDotNet 0.14.0 short run, 2026-11-07, .NET 8 on Apple M3 Ultra. Raw output is in docs/benchmarks/results.",
            Measurements = new List<MeasuredScenario>
            {
                new() { Scenario = "Flat object, 10 properties", Measured = "30.2 ns (AutoMapper 16.3.0: 46.1 ns, original engine: 1,000.8 ns)", PreviouslyClaimed = "8.45 ns" },
                new() { Scenario = "Nested order with customer, address and 3 lines", Measured = "154.2 ns (AutoMapper 16.3.0: 112.2 ns, original engine: 102,495.9 ns)", PreviouslyClaimed = "86.1 ns" },
                new() { Scenario = "List of 1,000 flat objects", Measured = "25.3 µs (AutoMapper 16.3.0: 19.6 µs, original engine: 14,002.6 µs)", PreviouslyClaimed = "5.05 µs" },
                new() { Scenario = "One MapSource expression", Measured = "29.5 ns (AutoMapper 16.3.0: 43.6 ns, original engine: 94,400.6 ns)", PreviouslyClaimed = "not shown" }
            },
            NextSteps = new List<string>
            {
                "Task 9.4 (done): BenchmarkDotNet baseline against hand-written code, Mapster and AutoMapper 16.3.0.",
                "Task 11.6 (done): the same benchmarks on the compiled engine. Every scenario is within 1.5 times of AutoMapper 16.3.0, and two are faster.",
                "Phase 15 (done): a source generator for eligible maps. They start about 45% faster and run under Native AOT."
            }
        };

        return View(model);
    }

    public IActionResult NativeAot()
    {
        var model = new NativeAotStatusViewModel
        {
            DotNetVersion = Environment.Version.ToString(),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            IsDynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported,
            IsDynamicCodeCompiled = RuntimeFeature.IsDynamicCodeCompiled,
            AutoMapSharpSupportsNativeAot = true,
            Blockers = new List<string>
            {
                "Only maps emitted by the source generator run under Native AOT. Other maps need expression trees, which Native AOT cannot compile.",
                "A map is generated when it uses conventions, flattening, Skip, UseValue, built-in conversions, nested maps and list or array collections.",
                "MapSource, resolvers, converters, conditions, callbacks, ForPath and constructor mapping keep a map on the compiled engine. The AMS100 build message says why.",
                "The library's reflection-based fallback still produces trim warnings when publishing with Native AOT."
            },
            Plans = ((MapEngine)_engine).DescribePlans()
                .Select(p => new PlanRow { Map = $"{p.Source.Name} -> {p.Destination.Name}", Kind = p.Plan.Kind.ToString(), Reason = p.Plan.Reason })
                .ToList()
        };

        return View(model);
    }
}
