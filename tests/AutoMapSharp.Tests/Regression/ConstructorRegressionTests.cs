using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ConstructorRegressionTests
{
    public class AddressEntity { public string City { get; set; } = ""; }
    public class LineEntity { public string Sku { get; set; } = ""; public int Quantity { get; set; } }

    public class OrderEntity
    {
        public int Id { get; set; }
        public AddressEntity? Shipping { get; set; }
        public AddressEntity? Billing { get; set; }
        public List<LineEntity> Lines { get; set; } = new();
    }

    public record AddressRecord(string City);
    public record LineRecord(string Sku, int Quantity);
    public record OrderRecord(int Id, AddressRecord Shipping, AddressRecord? Billing, IReadOnlyList<LineRecord> Lines);

    public class RecordModule : MappingModule
    {
        public RecordModule()
        {
            DefineMap<AddressEntity, AddressRecord>();
            DefineMap<LineEntity, LineRecord>();
            DefineMap<OrderEntity, OrderRecord>();
        }
    }

    [Fact]
    [Trait("Finding", "F09")]
    public void F09_RecordConstructor_MapsNestedRecordsAndCollections()
    {
        var engine = new MapEngine(new RecordModule());
        var entity = new OrderEntity
        {
            Id = 7,
            Shipping = new AddressEntity { City = "Oslo" },
            Billing = null,
            Lines = new() { new() { Sku = "A", Quantity = 2 }, new() { Sku = "B", Quantity = 1 } }
        };

        var result = engine.Map<OrderEntity, OrderRecord>(entity);

        Assert.Equal(7, result.Id);
        Assert.Equal("Oslo", result.Shipping.City);
        Assert.Null(result.Billing);
        Assert.Equal(new[] { "A", "B" }, result.Lines.Select(l => l.Sku));
        Assert.Equal(2, result.Lines[0].Quantity);
    }
}
