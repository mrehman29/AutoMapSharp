using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapSharp.Configuration;
using AutoMapSharp.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.Tests;

/// <summary>
/// AutoMapper parity features from remediation Phase 13.
/// </summary>
public class ParityTier1Tests
{
    public class Person { public string Name { get; set; } = ""; public int Age { get; set; } public string? Nickname { get; set; } }
    public class PersonDto { public string Name { get; set; } = ""; public int Age { get; set; } public string? Nickname { get; set; } }

    // 13.1 Inline configuration ---------------------------------------------

    public class InlineAddedModule : MappingModule
    {
        public InlineAddedModule() { DefineMap<PersonDto, Person>(); }
    }

    [Fact]
    public void T13_1_InlineConfiguration_DefinesMapsAndAddsModules()
    {
        var config = new MappingConfiguration(cfg =>
        {
            cfg.DefineMap<Person, PersonDto>().ForProperty(d => d.Nickname, o => o.UseValue("inline"));
            cfg.AddModule<InlineAddedModule>();
        });

        config.AssertConfigurationIsValid();
        var engine = config.CreateEngine();

        Assert.Equal("inline", engine.Map<Person, PersonDto>(new Person { Name = "a" }).Nickname);
        Assert.Equal("b", engine.Map<PersonDto, Person>(new PersonDto { Name = "b" }).Name);
        Assert.Equal(2, config.Modules.Count);
    }

    [Fact]
    public void T13_1_MapEngine_AcceptsInlineConfiguration()
    {
        var engine = new MapEngine(cfg =>
        {
            cfg.ValueTransformers.Add<string>(s => s?.ToUpperInvariant()!);
            cfg.DefineMap<Person, PersonDto>();
        });

        Assert.Equal("ANN", engine.Map<Person, PersonDto>(new Person { Name = "ann" }).Name);
    }

    // 13.2 Fields and ShouldMap hooks ---------------------------------------

    public class FieldSource { public string Code = "X1"; public int Count = 3; public string GetSecret() => "secret"; public string Internal { get; set; } = "i"; }
    public class FieldTarget { public string Code = ""; public int Count; public string? Secret { get; set; } public string? Internal { get; set; } }

    public class FieldModule : MappingModule
    {
        public FieldModule() { DefineMap<FieldSource, FieldTarget>(); }
    }

    public class HookModule : MappingModule
    {
        public HookModule()
        {
            ShouldMapMethod = m => !m.Name.Contains("Secret");
            ShouldMapProperty = p => p.Name != "Internal";
            DefineMap<FieldSource, FieldTarget>();
        }
    }

    [Fact]
    public void T13_2_PublicFields_AreMappedOnBothSides()
    {
        var result = new MapEngine(new FieldModule()).Map<FieldSource, FieldTarget>(new FieldSource());

        Assert.Equal("X1", result.Code);
        Assert.Equal(3, result.Count);
        Assert.Equal("secret", result.Secret);
    }

    [Fact]
    public void T13_2_ShouldMapHooks_ExcludeMembers()
    {
        var result = new MapEngine(new HookModule()).Map<FieldSource, FieldTarget>(new FieldSource());

        Assert.Null(result.Secret);
        Assert.Null(result.Internal);
        Assert.Equal("X1", result.Code);
    }

    // 13.3 / 13.4 Conditions ------------------------------------------------

    public class CountingResolver : IPropertyResolver<Person, PersonDto, string?>
    {
        public static int Calls;
        public string? Resolve(Person source, PersonDto destination, ResolutionContext context) { Calls++; return "resolved"; }
    }

    public class ConditionModule : MappingModule
    {
        public ConditionModule()
        {
            DefineMap<Person, PersonDto>()
                .ForProperty(d => d.Nickname, o => o.PreCondition(s => s.Age >= 18).MapWith<CountingResolver>())
                .ForProperty(d => d.Name, o => o.If((s, d, value) => value.Length > 1))
                .ForProperty(d => d.Age, o => o.If((s, d, value, current) => value > current));
        }
    }

    [Fact]
    public void T13_3_PreCondition_SkipsResolverWhenFalse()
    {
        var engine = new MapEngine(new ConditionModule());
        CountingResolver.Calls = 0;

        var minor = engine.Map<Person, PersonDto>(new Person { Name = "kid", Age = 10 });
        var adult = engine.Map<Person, PersonDto>(new Person { Name = "adult", Age = 30 });

        Assert.Null(minor.Nickname);
        Assert.Equal("resolved", adult.Nickname);
        Assert.Equal(1, CountingResolver.Calls);
    }

    [Fact]
    public void T13_4_ValueConditions_SeeNewAndCurrentValues()
    {
        var engine = new MapEngine(new ConditionModule());
        var target = new PersonDto { Name = "keep", Age = 50 };

        engine.Map(new Person { Name = "x", Age = 40 }, target);

        Assert.Equal("keep", target.Name); // "x" is too short
        Assert.Equal(50, target.Age);      // 40 is not greater than 50

        engine.Map(new Person { Name = "longer", Age = 60 }, target);
        Assert.Equal("longer", target.Name);
        Assert.Equal(60, target.Age);
    }

    // 13.5 UseDestinationValue ----------------------------------------------

    public class Settings { public string Theme { get; set; } = ""; }
    public class SettingsDto { public string Theme { get; set; } = ""; }
    public class Account { public Settings? Settings { get; set; } public Settings? Defaults { get; set; } }
    public class AccountDto { public SettingsDto? Settings { get; set; } public SettingsDto? Defaults { get; set; } }

    public class DestinationValueModule : MappingModule
    {
        public DestinationValueModule()
        {
            DefineMap<Settings, SettingsDto>();
            DefineMap<Account, AccountDto>()
                .ForProperty(d => d.Settings, o => o.UseDestinationValue())
                .ForProperty(d => d.Defaults, o => o.DoNotUseDestinationValue());
        }
    }

    [Fact]
    public void T13_5_UseDestinationValue_KeepsExistingObject()
    {
        var engine = new MapEngine(new DestinationValueModule());
        var settings = new SettingsDto { Theme = "old" };
        var defaults = new SettingsDto { Theme = "old" };
        var target = new AccountDto { Settings = settings, Defaults = defaults };

        engine.Map(new Account { Settings = new Settings { Theme = "dark" }, Defaults = new Settings { Theme = "light" } }, target);
        Assert.Same(settings, target.Settings);
        Assert.Equal("dark", settings.Theme);
        Assert.NotSame(defaults, target.Defaults);
        Assert.Equal("light", target.Defaults!.Theme);

        engine.Map(new Account { Settings = null }, target);
        Assert.Same(settings, target.Settings);
    }

    // 13.6 Per-call options --------------------------------------------------

    public class TenantResolver : IPropertyResolver<Person, PersonDto, string?>
    {
        public string? Resolve(Person source, PersonDto destination, ResolutionContext context) => context.Items["tenant"] as string;
    }

    public class ItemsModule : MappingModule
    {
        public ItemsModule() { DefineMap<Person, PersonDto>().ForProperty(d => d.Nickname, o => o.MapWith<TenantResolver>()); }
    }

    [Fact]
    public void T13_6_PerCallOptions_PassItemsAndRunCallbacks()
    {
        var engine = new MapEngine(new ItemsModule());
        var log = new List<string>();

        var result = engine.Map<Person, PersonDto>(new Person { Name = "a" }, opt =>
        {
            opt.Items["tenant"] = "acme";
            opt.BeforeMap((s, d) => log.Add("before:" + d.Name));
            opt.AfterMap((s, d) => log.Add("after:" + d.Name));
        });

        Assert.Equal("acme", result.Nickname);
        Assert.Equal(new[] { "before:", "after:a" }, log);
        Assert.Equal("globex", engine.Map<PersonDto>(new Person(), opt => opt.Items["tenant"] = "globex").Nickname);
    }

    // 13.7 ConstructUsing with context and ForCtorParam ----------------------

    public class Ticket
    {
        public Ticket(string code, string owner, int priority) { Code = code; Owner = owner; Priority = priority; }
        public string Code { get; }
        public string Owner { get; }
        public int Priority { get; }
    }

    public class TicketSource { public string Reference { get; set; } = ""; public string Owner { get; set; } = ""; }

    public class CtorModule : MappingModule
    {
        public CtorModule()
        {
            DefineMap<TicketSource, Ticket>()
                .ForCtorParam("code", o => o.MapSource(s => "T-" + s.Reference))
                .ForCtorParam("priority", o => o.UseValue(2));
            // Members are still mapped after ConstructUsing, so Nickname is skipped to keep the constructed value.
            DefineMap<Person, PersonDto>()
                .ConstructUsing((s, ctx) => new PersonDto { Nickname = (string?)ctx.Items["tag"] })
                .ForProperty(d => d.Nickname, o => o.Skip());
        }
    }

    [Fact]
    public void T13_7_ForCtorParam_And_ConstructUsingWithContext()
    {
        var engine = new MapEngine(new CtorModule());

        var ticket = engine.Map<TicketSource, Ticket>(new TicketSource { Reference = "9", Owner = "ann" });
        var person = engine.Map<Person, PersonDto>(new Person { Name = "a" }, opt => opt.Items["tag"] = "from-context");

        Assert.Equal("T-9", ticket.Code);
        Assert.Equal("ann", ticket.Owner);
        Assert.Equal(2, ticket.Priority);
        Assert.Equal("from-context", person.Nickname);
        Assert.Equal("a", person.Name);
    }

    // 13.8 Mapping actions ----------------------------------------------------

    public interface IClock { DateTime Now { get; } }
    public class FixedClock : IClock { public DateTime Now => new(2026, 11, 7); }
    public class Stamped { public string Name { get; set; } = ""; }
    public class StampedDto { public string Name { get; set; } = ""; public DateTime StampedAt { get; set; } }

    public class StampAction : IMappingAction<Stamped, StampedDto>
    {
        private readonly IClock _clock;
        public StampAction(IClock clock) { _clock = clock; }
        public void Process(Stamped source, StampedDto destination, ResolutionContext context) => destination.StampedAt = _clock.Now;
    }

    public class ActionModule : MappingModule
    {
        public ActionModule() { DefineMap<Stamped, StampedDto>().ForProperty(d => d.StampedAt, o => o.Skip()).AfterMap<StampAction>(); }
    }

    [Fact]
    public void T13_8_MappingAction_GetsDependenciesFromServices()
    {
        var services = new ServiceCollection().AddSingleton<IClock, FixedClock>().BuildServiceProvider();
        var engine = new MapEngine(new[] { new ActionModule() }, services);

        Assert.Equal(new DateTime(2026, 11, 7), engine.Map<Stamped, StampedDto>(new Stamped()).StampedAt);
    }

    // 13.9 / 13.10 Value converters and per-member transforms -----------------

    public class CentsToCurrency : IValueConverter<long, string>
    {
        public string Convert(long sourceMember, ResolutionContext context) => (sourceMember / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    public class Invoice { public long Total { get; set; } public long TaxCents { get; set; } public string Note { get; set; } = ""; }
    public class InvoiceDto { public string Total { get; set; } = ""; public string Tax { get; set; } = ""; public string Note { get; set; } = ""; }

    public class ConverterModule : MappingModule
    {
        public ConverterModule()
        {
            DefineMap<Invoice, InvoiceDto>()
                .ForProperty(d => d.Total, o => o.ConvertUsing<CentsToCurrency>())
                .ForProperty(d => d.Tax, o => o.ConvertUsing<CentsToCurrency, long>(s => s.TaxCents))
                .ForProperty(d => d.Note, o => o.AddTransform(n => n.Trim()).AddTransform(n => n + "!"));
        }
    }

    [Fact]
    public void T13_9_ValueConverter_And_T13_10_AddTransform()
    {
        var result = new MapEngine(new ConverterModule()).Map<Invoice, InvoiceDto>(new Invoice { Total = 12345, TaxCents = 250, Note = "  paid  " });

        Assert.Equal("123.45", result.Total);
        Assert.Equal("2.50", result.Tax);
        Assert.Equal("paid!", result.Note);
    }

    // 13.11 IncludeAllDerived, As, multiple IncludeBase -----------------------

    public abstract class Animal { public string Name { get; set; } = ""; public string Sound { get; set; } = ""; }
    public class Dog : Animal { public bool Good { get; set; } }
    public class Puppy : Dog { public int Weeks { get; set; } }
    public abstract class AnimalDto { public string Name { get; set; } = ""; public string Sound { get; set; } = ""; }
    public class DogDto : AnimalDto { public bool Good { get; set; } }
    public class PuppyDto : DogDto { public int Weeks { get; set; } }

    public class AllDerivedModule : MappingModule
    {
        public AllDerivedModule()
        {
            DefineMap<Animal, AnimalDto>().IncludeAllDerived().ForProperty(d => d.Sound, o => o.UseValue("generic"));
            DefineMap<Dog, DogDto>();
            DefineMap<Puppy, PuppyDto>();
        }
    }

    [Fact]
    public void T13_11_IncludeAllDerived_DispatchesToClosestMapAndInheritsRules()
    {
        var engine = new MapEngine(new AllDerivedModule());

        var puppy = engine.Map<Animal, AnimalDto>(new Puppy { Name = "p", Weeks = 8 });
        var dog = engine.Map<Animal, AnimalDto>(new Dog { Name = "d", Good = true });

        Assert.Equal(8, Assert.IsType<PuppyDto>(puppy).Weeks);
        Assert.True(Assert.IsType<DogDto>(dog).Good);
        Assert.Equal("generic", puppy.Sound);
    }

    public class Vehicle { public string Make { get; set; } = ""; }
    public class VehicleDto { public string Make { get; set; } = ""; }
    public class CarDto : VehicleDto { public int Doors { get; set; } = 4; }

    public interface IAudited { DateTime Created { get; set; } }
    public class AuditSource { public DateTime Created { get; set; } public string Who { get; set; } = ""; }
    public class AuditBaseDto { public DateTime Created { get; set; } }
    public class OwnerSource : AuditSource { }
    public class OwnerBaseDto { public string Who { get; set; } = ""; }
    public class OwnedRecord : AuditSource { public string Title { get; set; } = ""; }
    public class OwnedRecordDto : AuditBaseDto { public string Who { get; set; } = ""; public string Title { get; set; } = ""; }

    public class AsAndBasesModule : MappingModule
    {
        public AsAndBasesModule()
        {
            DefineMap<Vehicle, VehicleDto>().As<CarDto>();
            DefineMap<Vehicle, CarDto>();

            DefineMap<AuditSource, AuditBaseDto>().ForProperty(d => d.Created, o => o.UseValue(new DateTime(2000, 1, 1)));
            DefineMap<AuditSource, OwnerBaseDto>().ForProperty(d => d.Who, o => o.UseValue("system"));
            DefineMap<OwnedRecord, OwnedRecordDto>()
                .IncludeBase<AuditSource, AuditBaseDto>()
                .IncludeBase<AuditSource, OwnerBaseDto>();
        }
    }

    [Fact]
    public void T13_11_As_RedirectsToMoreSpecificDestination()
    {
        var engine = new MapEngine(new AsAndBasesModule());

        var result = engine.Map<Vehicle, VehicleDto>(new Vehicle { Make = "Volvo" });

        Assert.Equal(4, Assert.IsType<CarDto>(result).Doors);
        Assert.Equal("Volvo", result.Make);
    }

    [Fact]
    public void T13_11_SeveralIncludeBase_MergeInOrder()
    {
        var engine = new MapEngine(new AsAndBasesModule());

        var result = engine.Map<OwnedRecord, OwnedRecordDto>(new OwnedRecord { Who = "ann", Title = "t", Created = DateTime.UtcNow });

        Assert.Equal(new DateTime(2000, 1, 1), result.Created);
        Assert.Equal("system", result.Who);
        Assert.Equal("t", result.Title);
    }

    // 13.12 Open generics and DefineMap(Type, Type) ---------------------------

    public class Page<T> { public List<T> Items { get; set; } = new(); public int Total { get; set; } public string Internal { get; set; } = ""; }
    public class PageDto<T> { public List<T> Items { get; set; } = new(); public int Count { get; set; } public string? Internal { get; set; } public string Source { get; set; } = ""; }

    public class Wrapper<T> { public T Value { get; set; } = default!; }
    public class Unwrapped<T> { public T Value { get; set; } = default!; }
    public class UnwrapConverter<T> : ITypeConverter<Wrapper<T>, Unwrapped<T>>
    {
        public Unwrapped<T> Convert(Wrapper<T> source, ResolutionContext context) => new() { Value = source.Value };
    }

    public class GenericModule : MappingModule
    {
        public GenericModule()
        {
            DefineMap(typeof(Page<>), typeof(PageDto<>))
                .ForProperty("Count", o => o.MapSource("Total"))
                .ForProperty("Internal", o => o.Skip())
                .ForProperty("Source", o => o.UseValue("generic"));
            DefineMap(typeof(Wrapper<>), typeof(Unwrapped<>)).ConvertUsing(typeof(UnwrapConverter<>));
            DefineMap(typeof(Person), typeof(PersonDto)).ForProperty(nameof(PersonDto.Nickname), o => o.MapSource(nameof(Person.Name)));
        }
    }

    [Fact]
    public void T13_12_OpenGenericMap_IsClosedForEachTypeArgument()
    {
        var engine = new MapEngine(new GenericModule());

        var ints = engine.Map<Page<int>, PageDto<int>>(new Page<int> { Items = { 1, 2 }, Total = 2, Internal = "x" });
        var people = engine.Map<Page<Person>, PageDto<Person>>(new Page<Person> { Items = { new Person { Name = "a" } }, Total = 1 });

        Assert.Equal(new[] { 1, 2 }, ints.Items);
        Assert.Equal(2, ints.Count);
        Assert.Null(ints.Internal);
        Assert.Equal("generic", ints.Source);
        Assert.Equal("a", people.Items[0].Name);
    }

    [Fact]
    public void T13_12_OpenGenericConverter_And_NonGenericDefineMap()
    {
        var engine = new MapEngine(new GenericModule());

        Assert.Equal(42, engine.Map<Wrapper<int>, Unwrapped<int>>(new Wrapper<int> { Value = 42 }).Value);
        Assert.Equal("ann", engine.Map<Person, PersonDto>(new Person { Name = "ann" }).Nickname);
    }

    // 13.13 Naming conventions --------------------------------------------------

    public class LegacyRow { public string GetName() => "method"; public string Name_Ä { get; set; } = "umlaut"; public string firstName { get; set; } = "camel"; }
    public class CleanRow { public string? DtoName { get; set; } public string? Name_A { get; set; } public string? FirstName { get; set; } }

    public class ConventionModule : MappingModule
    {
        public ConventionModule()
        {
            ClearPrefixes();
            RecognizeDestinationPrefixes("Dto");
            ReplaceMemberName("Ä", "A");
            DefineMap<LegacyRow, CleanRow>().ForProperty(d => d.DtoName, o => o.Skip());
        }
    }

    public class DestinationPrefixSource { public string Name { get; set; } = "n"; }
    public class DestinationPrefixModule : MappingModule
    {
        public DestinationPrefixModule()
        {
            RecognizeDestinationPrefixes("Dto");
            DefineMap<DestinationPrefixSource, CleanRow>();
        }
    }

    [Fact]
    public void T13_13_ReplaceMemberName_CamelCase_And_ClearPrefixes()
    {
        var result = new MapEngine(new ConventionModule()).Map<LegacyRow, CleanRow>(new LegacyRow());

        Assert.Equal("umlaut", result.Name_A);
        Assert.Equal("camel", result.FirstName);
    }

    [Fact]
    public void T13_13_DestinationPrefixes_AreStrippedForMatching()
    {
        var result = new MapEngine(new DestinationPrefixModule()).Map<DestinationPrefixSource, CleanRow>(new DestinationPrefixSource());

        Assert.Equal("n", result.DtoName);
    }

    public class GetterSource { public string GetCode() => "from-method"; }
    public class GetterTarget { public string? Code { get; set; } }
    public class ClearedModule : MappingModule
    {
        public ClearedModule() { ClearPrefixes(); DefineMap<GetterSource, GetterTarget>(); }
    }

    [Fact]
    public void T13_13_ClearPrefixes_StopsGetMethodMatching()
    {
        Assert.Null(new MapEngine(new ClearedModule()).Map<GetterSource, GetterTarget>(new GetterSource()).Code);
        Assert.Contains(nameof(GetterTarget.Code), new MapEngine(new ClearedModule()).ValidateConfiguration().Errors.Single().UnmappedProperties);
    }

    // 13.14 Validation ----------------------------------------------------------

    public class Wide { public string Name { get; set; } = ""; public string Extra { get; set; } = ""; public string Ignored { get; set; } = ""; }
    public class Narrow { public string Name { get; set; } = ""; }
    public class Holder { public Inner Inner { get; set; } = new(); }
    public class Inner { public string A { get; set; } = ""; }
    public class HolderDto { public InnerDto? Inner { get; set; } }
    public class InnerDto { public string A { get; set; } = ""; public string Missing { get; set; } = ""; }
    public class NeedsCtor { public NeedsCtor(string unknownThing) { UnknownThing = unknownThing; } public string UnknownThing { get; } }
    public abstract class Base { }
    public class Derived : Base { }
    public abstract class BaseDto { }
    public class DerivedDto : BaseDto { }

    public class ValidationModule : MappingModule
    {
        public ValidationModule()
        {
            DefineMap<Wide, Narrow>().ValidateMembers(MemberList.Source).ForSourceMember(s => s.Ignored, o => o.DoNotValidate());
            DefineMap<Holder, HolderDto>();
            DefineMap<Person, NeedsCtor>();
            DefineMap<Person, Ticket>().ForCtorParam("doesNotExist", o => o.UseValue(1));
            DefineMap<Base, BaseDto>().Include<Derived, DerivedDto>();
        }
    }

    [Fact]
    public void T13_14_Validation_ReportsEveryKindOfProblem()
    {
        var result = new MapEngine(new ValidationModule()).ValidateConfiguration();
        var report = result.GenerateReport();

        var wide = result.Errors.Single(e => e.SourceType == typeof(Wide));
        Assert.Equal(new[] { "Extra" }, wide.UnmappedSourceMembers);
        Assert.Empty(wide.UnmappedProperties);

        var nested = result.Errors.Single(e => e.SourceType == typeof(Inner));
        Assert.Contains("Missing", nested.UnmappedProperties);
        Assert.Contains("Inner", nested.Context);

        Assert.Contains(result.Errors, e => e.DestinationType == typeof(NeedsCtor) && e.Problems.Any(p => p.Contains("unknownThing")));
        Assert.Contains("doesNotExist", report);
        Assert.Contains("Include<Derived, DerivedDto>", report);
    }
}
