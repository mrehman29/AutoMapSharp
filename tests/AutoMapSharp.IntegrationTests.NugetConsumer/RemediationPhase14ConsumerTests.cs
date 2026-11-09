using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Collections;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoMapSharp.IntegrationTests.NugetConsumer;

/// <summary>
/// Checks more parity features through the packed NuGet packages, including AutoMapSharp.Collections
/// and projections translated by EF Core on SQLite.
/// </summary>
public class RemediationPhase14ConsumerTests
{
    public abstract class Vehicle { public int Id { get; set; } public string? Name { get; set; } }
    public class Car : Vehicle { public int Doors { get; set; } }
    public class Truck : Vehicle { public int Axles { get; set; } }
    public abstract class VehicleDto { public int Id { get; set; } public string Name { get; set; } = ""; }
    public class CarDto : VehicleDto { public int Doors { get; set; } }
    public class TruckDto : VehicleDto { public int Axles { get; set; } }

    public class FleetContext : DbContext
    {
        public FleetContext(DbContextOptions<FleetContext> options) : base(options) { }
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Car>();
            modelBuilder.Entity<Truck>();
        }
    }

    public class FleetModule : MappingModule
    {
        public FleetModule()
        {
            DefineMap<Vehicle, VehicleDto>()
                .Include<Car, CarDto>()
                .Include<Truck, TruckDto>()
                .ForProperty(d => d.Name, o => o.Fallback("(unnamed)"));
            DefineMap<Car, CarDto>().IncludeBase<Vehicle, VehicleDto>();
            DefineMap<Truck, TruckDto>().IncludeBase<Vehicle, VehicleDto>();
        }
    }

    [Fact]
    public void Package_Projection_TranslatesInheritanceAndFallbackToSql()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<FleetContext>().UseSqlite(connection).Options;
        using (var setup = new FleetContext(options))
        {
            setup.Database.EnsureCreated();
            setup.Vehicles.AddRange(new Car { Id = 1, Name = "Polo", Doors = 5 }, new Truck { Id = 2, Name = null, Axles = 3 });
            setup.SaveChanges();
        }

        using var db = new FleetContext(options);
        var query = db.Vehicles.OrderBy(v => v.Id).ProjectOnto<VehicleDto>(new MapEngine(new FleetModule()));
        var result = query.ToList();

        Assert.Equal(5, Assert.IsType<CarDto>(result[0]).Doors);
        Assert.Equal(3, Assert.IsType<TruckDto>(result[1]).Axles);
        Assert.Equal("(unnamed)", result[1].Name);
        Assert.Contains("COALESCE", query.ToQueryString());
    }

    public class LineDto { public int Sku { get; set; } public int Quantity { get; set; } }
    public class Line { public int Sku { get; set; } public int Quantity { get; set; } }
    public class Basket { public List<Line> Lines { get; set; } = new(); }
    public class BasketDto { public List<LineDto> Lines { get; set; } = new(); }

    public class BasketModule : MappingModule
    {
        public BasketModule()
        {
            DefineMap<LineDto, Line>().EqualityComparison((dto, line) => dto.Sku == line.Sku);
            DefineMap<BasketDto, Basket>();
        }
    }

    [Fact]
    public void Package_CollectionsPackage_MergesByKey()
    {
        var engine = new MapEngine(new BasketModule());
        var kept = new Line { Sku = 10, Quantity = 1 };
        var basket = new Basket { Lines = { kept, new Line { Sku = 20, Quantity = 1 } } };

        engine.Map(new BasketDto { Lines = { new LineDto { Sku = 10, Quantity = 4 }, new LineDto { Sku = 30, Quantity = 1 } } }, basket);

        Assert.Same(kept, basket.Lines[0]);
        Assert.Equal(4, kept.Quantity);
        Assert.Equal(new[] { 10, 30 }, basket.Lines.Select(l => l.Sku));
    }

    public class Contact { public string Name { get; set; } = ""; public int Age { get; set; } }

    [Fact]
    public void Package_ExpandoObjectRoundTrip()
    {
        var engine = new MapEngine();
        dynamic input = new ExpandoObject();
        input.name = "Ann";
        input.age = "30";

        var contact = engine.Map<Contact>((object)input);
        IDictionary<string, object?> output = engine.Map<Contact, ExpandoObject>(contact);

        Assert.Equal("Ann", contact.Name);
        Assert.Equal(30, contact.Age);
        Assert.Equal(30, output["Age"]);
    }
}
