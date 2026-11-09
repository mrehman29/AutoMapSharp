using System.Diagnostics;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

[Collection(NonParallelCollection.Name)]
public class PerformanceRegressionTests
{
    public class Person { public string First { get; set; } = "Ada"; public string Last { get; set; } = "Lovelace"; }
    public class PersonDto { public string FullName { get; set; } = ""; }

    public class MapSourceModule : MappingModule
    {
        public MapSourceModule()
        {
            DefineMap<Person, PersonDto>()
                .ForProperty(d => d.FullName, o => o.MapSource(s => s.First + " " + s.Last));
        }
    }

    [Fact]
    [Trait("Finding", "F21")]
    public void F21_MapSource_IsCompiledOnce_NotPerCall()
    {
        var engine = new MapEngine(new MapSourceModule());
        var person = new Person();
        for (int i = 0; i < 200; i++) engine.Map<Person, PersonDto>(person);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++) engine.Map<Person, PersonDto>(person);
        sw.Stop();

        Assert.Equal("Ada Lovelace", engine.Map<Person, PersonDto>(person).FullName);
        // Compiling once makes this take a few ms. Compiling per call took about a second when this test was written.
        Assert.True(sw.ElapsedMilliseconds < 50, $"10,000 maps with one MapSource took {sw.ElapsedMilliseconds} ms.");
    }
}
