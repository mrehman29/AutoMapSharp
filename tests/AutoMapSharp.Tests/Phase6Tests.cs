using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapSharp.Configuration;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase6Tests
{
    public class CustomerEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public AddressEntity? Address { get; set; }
    }

    public class AddressEntity
    {
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }

    public class OrderItemEntity
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Qty { get; set; }
    }

    public class OrderEntity
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public CustomerEntity? Customer { get; set; }
        public List<OrderItemEntity> Items { get; set; } = new();
        public List<string> InternalAuditLogs { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal LineTotal { get; set; }
    }

    public class OrderSummaryDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerAddressCity { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string TenantBadge { get; set; } = string.Empty;
        public List<OrderItemDto>? Items { get; set; }
        public List<string>? InternalAuditLogs { get; set; }
    }

    public class ProjectionMappingModule : MappingModule
    {
        public ProjectionMappingModule()
        {
            DefineMap<OrderItemEntity, OrderItemDto>()
                .ForProperty(d => d.LineTotal, opt => opt.MapSource(s => s.Price * s.Qty));

            DefineMap<OrderEntity, OrderSummaryDto>()
                .ForProperty(d => d.TenantBadge, opt => opt.UseValue("CORP-CLOUD"))
                .ForProperty(d => d.InternalAuditLogs, opt => opt.ExplicitExpansion());
        }
    }

    private static IQueryable<OrderEntity> GetSampleOrders()
    {
        var orders = new List<OrderEntity>
        {
            new()
            {
                Id = 101,
                OrderNumber = "ORD-2026-001",
                TotalAmount = 450.00m,
                Customer = new CustomerEntity
                {
                    Id = 1,
                    Name = "Ada Lovelace",
                    Address = new AddressEntity { City = "London", ZipCode = "SW1" }
                },
                Items = new List<OrderItemEntity>
                {
                    new() { Id = 1, ProductName = "Analytical Engine Part A", Price = 150.00m, Qty = 2 },
                    new() { Id = 2, ProductName = "Perforated Punch Card Set", Price = 150.00m, Qty = 1 }
                },
                InternalAuditLogs = new List<string> { "Created by Admin", "Processed Payment" }
            },
            new()
            {
                Id = 102,
                OrderNumber = "ORD-2026-002",
                TotalAmount = 1200.00m,
                Customer = new CustomerEntity
                {
                    Id = 2,
                    Name = "Alan Turing",
                    Address = new AddressEntity { City = "Bletchley", ZipCode = "MK3" }
                },
                Items = new List<OrderItemEntity>
                {
                    new() { Id = 3, ProductName = "Bombe Decryption Valve", Price = 600.00m, Qty = 2 }
                },
                InternalAuditLogs = new List<string> { "Classified Military Order" }
            }
        };

        return orders.AsQueryable();
    }

    [Fact]
    public void Task6_1_ProjectOnto_FlattensAndMapsProperties()
    {
        var engine = new MapEngine(new[] { new ProjectionMappingModule() });
        var query = GetSampleOrders().ProjectOnto<OrderSummaryDto>(engine);

        var list = query.ToList();

        Assert.Equal(2, list.Count);
        var order1 = list[0];
        Assert.Equal(101, order1.Id);
        Assert.Equal("ORD-2026-001", order1.OrderNumber);
        Assert.Equal(450.00m, order1.TotalAmount);
        Assert.Equal("Ada Lovelace", order1.CustomerName);
        Assert.Equal("London", order1.CustomerAddressCity);
        Assert.Equal("CORP-CLOUD", order1.TenantBadge);
    }

    [Fact]
    public void Task6_2_ProjectOnto_ProjectsNestedCollections()
    {
        var engine = new MapEngine(new[] { new ProjectionMappingModule() });
        var query = GetSampleOrders().ProjectOnto<OrderSummaryDto>(engine);

        var list = query.ToList();
        var order1 = list[0];

        Assert.NotNull(order1.Items);
        Assert.Equal(2, order1.Items.Count);
        Assert.Equal("Analytical Engine Part A", order1.Items[0].ProductName);
        Assert.Equal(300.00m, order1.Items[0].LineTotal);
        Assert.Equal(150.00m, order1.Items[1].LineTotal);
    }

    [Fact]
    public void Task6_3_ProjectOnto_ExplicitExpansion_OmittedByDefault()
    {
        var engine = new MapEngine(new[] { new ProjectionMappingModule() });

        // Without membersToExpand: InternalAuditLogs should be omitted (null)
        var queryDefault = GetSampleOrders().ProjectOnto<OrderSummaryDto>(engine);
        var orderDefault = queryDefault.First();
        Assert.Null(orderDefault.InternalAuditLogs);

        // With membersToExpand: InternalAuditLogs should be included
        var queryExpanded = GetSampleOrders().ProjectOnto<OrderSummaryDto>(engine, null, nameof(OrderSummaryDto.InternalAuditLogs));
        var orderExpanded = queryExpanded.First();
        Assert.NotNull(orderExpanded.InternalAuditLogs);
        Assert.Equal(2, orderExpanded.InternalAuditLogs.Count);
    }
}
