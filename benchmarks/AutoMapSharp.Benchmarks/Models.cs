using System;
using System.Collections.Generic;

namespace AutoMapSharp.Benchmarks;

public class FlatSource
{
    public int Id { get; set; } = 101;
    public string FirstName { get; set; } = "Ada";
    public string LastName { get; set; } = "Lovelace";
    public string Email { get; set; } = "ada@example.com";
    public int Age { get; set; } = 36;
    public decimal Balance { get; set; } = 1250.75m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = new(2026, 1, 1);
    public string Role { get; set; } = "Admin";
    public string Country { get; set; } = "UK";
}

public class FlatDestination
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Role { get; set; } = "";
    public string Country { get; set; } = "";
}

public class Address
{
    public string Street { get; set; } = "1 Main St";
    public string City { get; set; } = "Oslo";
    public string PostalCode { get; set; } = "0150";
}

public class Customer
{
    public int Id { get; set; } = 7;
    public string Name { get; set; } = "Ada Lovelace";
    public Address Address { get; set; } = new();
}

public class OrderLine
{
    public string Sku { get; set; } = "SKU-1";
    public int Quantity { get; set; } = 2;
    public decimal UnitPrice { get; set; } = 9.99m;
}

public class Order
{
    public int Id { get; set; } = 1001;
    public DateTime PlacedAt { get; set; } = new(2026, 2, 3);
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new() { new(), new() { Sku = "SKU-2", Quantity = 1 }, new() { Sku = "SKU-3" } };
}

public class AddressDto
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string PostalCode { get; set; } = "";
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public AddressDto Address { get; set; } = new();
}

public class OrderLineDto
{
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public CustomerDto Customer { get; set; } = new();
    public List<OrderLineDto> Lines { get; set; } = new();
}

public class PersonSource
{
    public string First { get; set; } = "Ada";
    public string Last { get; set; } = "Lovelace";
    public int Id { get; set; } = 5;
}

public class PersonDestination
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
}
