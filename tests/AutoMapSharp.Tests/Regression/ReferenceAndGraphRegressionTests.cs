using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ReferenceAndGraphRegressionTests
{
    public class Person { public string Name { get; set; } = ""; }
    public class PersonDto { public string Name { get; set; } = ""; }
    public class PersonSummary { public string Name { get; set; } = ""; }

    public class Holder
    {
        public Person? First { get; set; }
        public Person? Second { get; set; }
        public Person? AsSummary { get; set; }
    }

    public class HolderDto
    {
        public PersonDto? First { get; set; }
        public PersonDto? Second { get; set; }
        public PersonSummary? AsSummary { get; set; }
    }

    public class PreserveModule : MappingModule
    {
        public PreserveModule()
        {
            DefineMap<Holder, HolderDto>().PreserveReferences();
            DefineMap<Person, PersonDto>().PreserveReferences();
            DefineMap<Person, PersonSummary>().PreserveReferences();
        }
    }

    [Fact]
    [Trait("Finding", "F08")]
    public void F08_OneSourceMappedToTwoDestinationTypes_GetsCorrectTypes()
    {
        var engine = new MapEngine(new PreserveModule());
        var person = new Person { Name = "Ann" };

        var result = engine.Map<Holder, HolderDto>(new Holder { First = person, Second = person, AsSummary = person });

        Assert.IsType<PersonDto>(result.First);
        Assert.IsType<PersonSummary>(result.AsSummary);
        Assert.Equal("Ann", result.AsSummary!.Name);
    }

    [Fact]
    [Trait("Finding", "F08")]
    public void F08_RepeatedReference_IsSharedWithinSameDestinationType()
    {
        var engine = new MapEngine(new PreserveModule());
        var person = new Person { Name = "Ann" };

        var result = engine.Map<Holder, HolderDto>(new Holder { First = person, Second = person });

        Assert.Same(result.First, result.Second);
    }

    // F16: IncludeBase must not mutate shared configuration at map time.
    public class BaseEntity { public int Id { get; set; } }
    public class DerivedEntity : BaseEntity { public string Name { get; set; } = ""; }
    public class BaseDto { public int Id { get; set; } public List<string> Log { get; set; } = new(); }
    public class DerivedDto : BaseDto { public string Name { get; set; } = ""; }

    public class IncludeBaseModule : MappingModule
    {
        public IncludeBaseModule()
        {
            DefineMap<BaseEntity, BaseDto>()
                .BeforeMap((s, d) => d.Log.Add("base-before-1"))
                .BeforeMap((s, d) => d.Log.Add("base-before-2"))
                .AfterMap((s, d) => d.Log.Add("base-after"));

            DefineMap<DerivedEntity, DerivedDto>()
                .IncludeBase<BaseEntity, BaseDto>()
                .AfterMap((s, d) => d.Log.Add("derived-after"));
        }
    }

    [Fact]
    [Trait("Finding", "F16")]
    public void F16_IncludeBase_RunsBaseActionsOnceAndInDeclaredOrder()
    {
        var engine = new MapEngine(new IncludeBaseModule());

        var result = engine.Map<DerivedEntity, DerivedDto>(new DerivedEntity { Id = 1, Name = "n" });

        Assert.Equal(new[] { "base-before-1", "base-before-2", "base-after", "derived-after" }, result.Log);
    }

    [Fact]
    [Trait("Finding", "F16")]
    public async Task F16_IncludeBase_ParallelFirstMaps_AreConsistent()
    {
        var expected = new[] { "base-before-1", "base-before-2", "base-after", "derived-after" };
        var failures = new ConcurrentBag<string>();

        for (int round = 0; round < 20; round++)
        {
            var engine = new MapEngine(new IncludeBaseModule());
            using var start = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() =>
            {
                start.Wait();
                var dto = engine.Map<DerivedEntity, DerivedDto>(new DerivedEntity { Id = i, Name = "n" });
                if (dto.Id != i || !dto.Log.SequenceEqual(expected))
                {
                    failures.Add(string.Join(",", dto.Log));
                }
            })).ToArray();

            start.Set();
            await Task.WhenAll(tasks);
        }

        Assert.Empty(failures);
    }
}
