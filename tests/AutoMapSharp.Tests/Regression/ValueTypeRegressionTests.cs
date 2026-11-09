using System;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ValueTypeRegressionTests
{
    public enum Level { Low, High }

    public class NullableSource
    {
        public int? Int { get; set; }
        public long? Long { get; set; }
        public decimal? Decimal { get; set; }
        public double? Double { get; set; }
        public bool? Bool { get; set; }
        public Guid? Guid { get; set; }
        public DateTime? DateTime { get; set; }
        public DateTimeOffset? DateTimeOffset { get; set; }
        public Level? Level { get; set; }
    }

    public class NullableDestination
    {
        public int? Int { get; set; }
        public long? Long { get; set; }
        public decimal? Decimal { get; set; }
        public double? Double { get; set; }
        public bool? Bool { get; set; }
        public Guid? Guid { get; set; }
        public DateTime? DateTime { get; set; }
        public DateTimeOffset? DateTimeOffset { get; set; }
        public Level? Level { get; set; }
    }

    public class IntSource { public int Value { get; set; } }
    public class NullableIntSource { public int? Value { get; set; } }
    public class IntDestination { public int Value { get; set; } }
    public class NullableIntDestination { public int? Value { get; set; } }
    public class IntWithFallbackDestination { public int Value { get; set; } }

    public readonly struct Money
    {
        public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
        public decimal Amount { get; }
        public string Currency { get; }
    }

    public record struct Point(int X, int Y);

    public class StructSource
    {
        public TimeSpan Duration { get; set; }
        public DateOnly Day { get; set; }
        public TimeOnly Time { get; set; }
        public Money Price { get; set; }
        public Point Location { get; set; }
    }

    public class StructDestination
    {
        public TimeSpan Duration { get; set; }
        public DateOnly Day { get; set; }
        public TimeOnly Time { get; set; }
        public Money Price { get; set; }
        public Point Location { get; set; }
    }

    public class ObjectHolder { public object? Payload { get; set; } }
    public class ObjectHolderDto { public object? Payload { get; set; } }

    public class Address { public string City { get; set; } = ""; }
    public class Customer { public Address? Address { get; set; } }
    public class CustomerDto { public Address? Address { get; set; } }

    public class ValueTypeModule : MappingModule
    {
        public ValueTypeModule()
        {
            DefineMap<NullableSource, NullableDestination>();
            DefineMap<IntSource, NullableIntDestination>();
            DefineMap<NullableIntSource, IntDestination>();
            DefineMap<NullableIntSource, IntWithFallbackDestination>()
                .ForProperty(d => d.Value, o => o.Fallback(5));
            DefineMap<StructSource, StructDestination>();
            DefineMap<ObjectHolder, ObjectHolderDto>();
            DefineMap<Customer, CustomerDto>();
        }
    }

    public class CloneModule : MappingModule
    {
        public CloneModule()
        {
            DefineMap<Customer, CustomerDto>();
            DefineMap<Address, Address>();
        }
    }

    private static readonly IMapEngine Engine = new MapEngine(new ValueTypeModule());

    [Fact]
    [Trait("Finding", "F01")]
    public void F01_NullableValueTypes_WithValues_ArePreserved()
    {
        var source = new NullableSource
        {
            Int = 42,
            Long = 9_000_000_000L,
            Decimal = 19.95m,
            Double = 3.25,
            Bool = true,
            Guid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
            DateTime = new DateTime(2024, 5, 6, 7, 8, 9),
            DateTimeOffset = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(2)),
            Level = Level.High
        };

        var result = Engine.Map<NullableSource, NullableDestination>(source);

        Assert.Equal(source.Int, result.Int);
        Assert.Equal(source.Long, result.Long);
        Assert.Equal(source.Decimal, result.Decimal);
        Assert.Equal(source.Double, result.Double);
        Assert.Equal(source.Bool, result.Bool);
        Assert.Equal(source.Guid, result.Guid);
        Assert.Equal(source.DateTime, result.DateTime);
        Assert.Equal(source.DateTimeOffset, result.DateTimeOffset);
        Assert.Equal(source.Level, result.Level);
    }

    [Fact]
    [Trait("Finding", "F01")]
    public void F01_NullableValueTypes_WithNulls_StayNull()
    {
        var result = Engine.Map<NullableSource, NullableDestination>(new NullableSource());

        Assert.Null(result.Int);
        Assert.Null(result.Long);
        Assert.Null(result.Decimal);
        Assert.Null(result.Double);
        Assert.Null(result.Bool);
        Assert.Null(result.Guid);
        Assert.Null(result.DateTime);
        Assert.Null(result.DateTimeOffset);
        Assert.Null(result.Level);
    }

    [Fact]
    [Trait("Finding", "F01")]
    public void F01_IntToNullableInt_PreservesValue()
    {
        var result = Engine.Map<IntSource, NullableIntDestination>(new IntSource { Value = 42 });
        Assert.Equal(42, result.Value);
    }

    [Fact]
    [Trait("Finding", "F01")]
    public void F01_NullableIntToInt_PreservesValue()
    {
        var result = Engine.Map<NullableIntSource, IntDestination>(new NullableIntSource { Value = 42 });
        Assert.Equal(42, result.Value);
    }

    [Fact]
    [Trait("Finding", "F01")]
    public void F01_NullToInt_WithoutFallback_IsZero()
    {
        var result = Engine.Map<NullableIntSource, IntDestination>(new NullableIntSource { Value = null });
        Assert.Equal(0, result.Value);
    }

    [Fact]
    [Trait("Finding", "F01")]
    public void F01_NullToInt_WithFallback_UsesFallback()
    {
        var result = Engine.Map<NullableIntSource, IntWithFallbackDestination>(new NullableIntSource { Value = null });
        Assert.Equal(5, result.Value);
    }

    [Fact]
    [Trait("Finding", "F02")]
    public void F02_StructProperties_ArePreserved()
    {
        var source = new StructSource
        {
            Duration = TimeSpan.FromMinutes(90),
            Day = new DateOnly(2024, 5, 6),
            Time = new TimeOnly(13, 45, 10),
            Price = new Money(12.5m, "EUR"),
            Location = new Point(3, 4)
        };

        var result = Engine.Map<StructSource, StructDestination>(source);

        Assert.Equal(source.Duration, result.Duration);
        Assert.Equal(source.Day, result.Day);
        Assert.Equal(source.Time, result.Time);
        Assert.Equal(12.5m, result.Price.Amount);
        Assert.Equal("EUR", result.Price.Currency);
        Assert.Equal(source.Location, result.Location);
    }

    public static TheoryData<object> ObjectPayloads() => new()
    {
        "hello",
        42,
        new Address { City = "Oslo" }
    };

    [Theory]
    [MemberData(nameof(ObjectPayloads))]
    [Trait("Finding", "F07")]
    public void F07_ObjectProperty_KeepsSameInstance(object payload)
    {
        var result = Engine.Map<ObjectHolder, ObjectHolderDto>(new ObjectHolder { Payload = payload });
        Assert.Same(payload, result.Payload);
    }

    [Fact]
    [Trait("Finding", "F18")]
    public void F18_SameTypeNestedObject_WithoutMap_AssignsReference()
    {
        var address = new Address { City = "Oslo" };
        var result = Engine.Map<Customer, CustomerDto>(new Customer { Address = address });
        Assert.Same(address, result.Address);
    }

    [Fact]
    [Trait("Finding", "F18")]
    public void F18_SameTypeNestedObject_WithExplicitMap_CreatesNewInstance()
    {
        var engine = new MapEngine(new CloneModule());
        var address = new Address { City = "Oslo" };

        var result = engine.Map<Customer, CustomerDto>(new Customer { Address = address });

        Assert.NotNull(result.Address);
        Assert.NotSame(address, result.Address);
        Assert.Equal("Oslo", result.Address!.City);
    }
}
