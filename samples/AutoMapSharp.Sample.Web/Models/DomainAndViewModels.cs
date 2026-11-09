using System;

namespace AutoMapSharp.Sample.Web.Models;

public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Cancelled = 3
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class Customer
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Address? Address { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public Customer? Customer { get; set; }
    public OrderStatus Status { get; set; }
    public Guid TrackingCode { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; private set; }

    public string GetSpecialInstructions() => "Signature required upon delivery.";

    public void SetTotalAmount(decimal amount) => TotalAmount = amount;
}

public class OrderSummaryViewModel
{
    public int Id { get; set; }
    public string? CustomerName { get; set; }          // Deep Flattened from Customer.Name
    public string? CustomerAddressCity { get; set; }  // Deep Flattened from Customer.Address.City
    public string? SpecialInstructions { get; set; }   // Flattened from GetSpecialInstructions()
    public string? Status { get; set; }                // Converted from Enum to String
    public string? TrackingCode { get; set; }         // Converted from Guid to String
    public DateTimeOffset OrderDate { get; set; }     // Converted from DateTime to DateTimeOffset
    public decimal TotalAmount { get; set; }          // Mapped into private setter
}

public class UpdateOrderViewModel
{
    public int OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
}

// External webhook model using snake_case
public class ExternalWebhookUser
{
    public string first_name { get; set; } = string.Empty;
    public string last_name { get; set; } = string.Empty;
    public int user_age { get; set; }
    public string email_address { get; set; } = string.Empty;
}

// Internal display ViewModel using PascalCase
public class InternalUserViewModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int UserAge { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
}

// Legacy catalog items with Hungarian-style prefix and suffix
public class LegacyProduct
{
    public string m_Title { get; set; } = string.Empty;
    public string CategoryModel { get; set; } = string.Empty;
    public decimal m_Price { get; set; }
}

public class ProductViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// C# 9+ Positional Record
public record WebCustomerRecord(int Id, string FullName, string Tier);

public class WebCustomerSource
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
}

public class ComparisonViewModel<TSource, TDest>
{
    public string FeatureTitle { get; set; } = string.Empty;
    public string FeatureDescription { get; set; } = string.Empty;
    public string MappingRuleCode { get; set; } = string.Empty;
    public TSource Source { get; set; } = default!;
    public TDest Destination { get; set; } = default!;
    public bool IsInPlaceUpdate { get; set; }
    public bool? ReferenceEqual { get; set; }
}
