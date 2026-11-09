using System;
using System.Collections.Generic;

namespace AutoMapSharp.Sample.Web.Models;

public class DbCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? AddressId { get; set; }
    public DbAddress? Address { get; set; }
}

public class DbAddress
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

public class DbOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public DbOrder? Order { get; set; }
}

public class DbAuditLog
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DbOrder? Order { get; set; }
}

public class DbOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDateUtc { get; set; }
    public int? CustomerId { get; set; }
    public DbCustomer? Customer { get; set; }
    public List<DbOrderItem> Items { get; set; } = new();
    public List<DbAuditLog> AuditLogs { get; set; } = new();
}

public class OrderItemProjectionDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderProjectionDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string TenantBadge { get; set; } = string.Empty;
    public List<OrderItemProjectionDto>? Items { get; set; }
    public List<string>? AuditLogs { get; set; }
}
