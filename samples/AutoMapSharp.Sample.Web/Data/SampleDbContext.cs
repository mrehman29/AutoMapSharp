using System;
using System.Collections.Generic;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoMapSharp.Sample.Web.Data;

public class SampleDbContext : DbContext
{
    public DbSet<DbOrder> Orders => Set<DbOrder>();
    public DbSet<DbCustomer> Customers => Set<DbCustomer>();
    public DbSet<DbAddress> Addresses => Set<DbAddress>();
    public DbSet<DbOrderItem> OrderItems => Set<DbOrderItem>();
    public DbSet<DbAuditLog> AuditLogs => Set<DbAuditLog>();

    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
    {
    }

    public void SeedInitialData()
    {
        if (Orders.Any()) return;

        var addr1 = new DbAddress { Id = 1, Street = "100 Innovation Way", City = "San Francisco", State = "CA", PostalCode = "94105" };
        var addr2 = new DbAddress { Id = 2, Street = "55 Silicon Blvd", City = "Austin", State = "TX", PostalCode = "78701" };
        var addr3 = new DbAddress { Id = 3, Street = "10 Downing St", City = "London", State = "UK", PostalCode = "SW1A" };

        Addresses.AddRange(addr1, addr2, addr3);

        var cust1 = new DbCustomer { Id = 1, Name = "Katherine Johnson", Email = "katherine.johnson@nasa.gov", Address = addr1 };
        var cust2 = new DbCustomer { Id = 2, Name = "Alan Turing", Email = "alan.turing@bletchley.ac.uk", Address = addr2 };
        var cust3 = new DbCustomer { Id = 3, Name = "Ada Lovelace", Email = "ada.lovelace@computing.org", Address = addr3 };

        Customers.AddRange(cust1, cust2, cust3);

        var order1 = new DbOrder
        {
            Id = 1,
            OrderNumber = "ORD-2026-8801",
            TotalAmount = 2450.00m,
            OrderDateUtc = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            Customer = cust1,
            Items = new List<DbOrderItem>
            {
                new() { Id = 1, ProductName = "Cryogenic Fuel Injector", UnitPrice = 850.00m, Quantity = 2 },
                new() { Id = 2, ProductName = "Thermal Tile Sealant Kit", UnitPrice = 750.00m, Quantity = 1 }
            },
            AuditLogs = new List<DbAuditLog>
            {
                new() { Id = 1, Message = "Order received and validated", CreatedAtUtc = new DateTime(2026, 11, 1, 10, 5, 0, DateTimeKind.Utc) },
                new() { Id = 2, Message = "Payment approved via corporate PO", CreatedAtUtc = new DateTime(2026, 11, 1, 10, 15, 0, DateTimeKind.Utc) }
            }
        };

        var order2 = new DbOrder
        {
            Id = 2,
            OrderNumber = "ORD-2026-8802",
            TotalAmount = 890.50m,
            OrderDateUtc = new DateTime(2026, 11, 2, 14, 30, 0, DateTimeKind.Utc),
            Customer = cust2,
            Items = new List<DbOrderItem>
            {
                new() { Id = 3, ProductName = "High-Density RAM Module (128GB)", UnitPrice = 445.25m, Quantity = 2 }
            },
            AuditLogs = new List<DbAuditLog>
            {
                new() { Id = 3, Message = "Cryptographic order signed with PGP", CreatedAtUtc = new DateTime(2026, 11, 2, 14, 35, 0, DateTimeKind.Utc) }
            }
        };

        var order3 = new DbOrder
        {
            Id = 3,
            OrderNumber = "ORD-2026-8803",
            TotalAmount = 5600.00m,
            OrderDateUtc = new DateTime(2026, 11, 3, 9, 15, 0, DateTimeKind.Utc),
            Customer = cust3,
            Items = new List<DbOrderItem>
            {
                new() { Id = 4, ProductName = "Quantum Matrix Accelerator", UnitPrice = 2800.00m, Quantity = 2 }
            },
            AuditLogs = new List<DbAuditLog>
            {
                new() { Id = 4, Message = "Export compliance clearance granted", CreatedAtUtc = new DateTime(2026, 11, 3, 9, 20, 0, DateTimeKind.Utc) }
            }
        };

        Orders.AddRange(order1, order2, order3);
        SaveChanges();
    }
}
