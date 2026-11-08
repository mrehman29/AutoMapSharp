using System;
using System.Collections.Generic;
using AutoMapSharp.Sample.Web.Models;

namespace AutoMapSharp.Sample.Web.Data;

/// <summary>
/// In-memory mock database providing hardcoded, realistic sample data for visual demonstrations.
/// </summary>
public static class MockDatabase
{
    private static readonly Dictionary<int, Order> _orders = new();

    static MockDatabase()
    {
        ResetData();
    }

    public static void ResetData()
    {
        _orders.Clear();

        var order1 = new Order
        {
            Id = 1001,
            Customer = new Customer
            {
                Name = "Acme Global Industries",
                Email = "procurement@acme.com",
                Address = new Address { Street = "500 Oracle Parkway", City = "Redwood Shores", ZipCode = "94065" }
            },
            Status = OrderStatus.Completed,
            TrackingCode = Guid.Parse("a81f38d4-5178-4e5a-939e-21ef65d83a12"),
            OrderDate = new DateTime(2026, 11, 4, 14, 30, 0, DateTimeKind.Utc)
        };
        order1.SetTotalAmount(1249.99m);
        _orders[order1.Id] = order1;

        var order2 = new Order
        {
            Id = 1002,
            Customer = new Customer
            {
                Name = "Starlight Technologies",
                Email = "orders@starlight.io",
                Address = new Address { Street = "101 Market Street", City = "San Francisco", ZipCode = "94105" }
            },
            Status = OrderStatus.Processing,
            TrackingCode = Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479"),
            OrderDate = new DateTime(2026, 11, 3, 9, 15, 0, DateTimeKind.Utc)
        };
        order2.SetTotalAmount(890.50m);
        _orders[order2.Id] = order2;

        var orderNullSafe = new Order
        {
            Id = 1003,
            Customer = null, // Intentional null to demonstrate null-safe flattening
            Status = OrderStatus.Pending,
            TrackingCode = Guid.Parse("c9b6f882-2736-4074-b5a7-7a55f848971b"),
            OrderDate = new DateTime(2026, 11, 4, 18, 0, 0, DateTimeKind.Utc)
        };
        orderNullSafe.SetTotalAmount(0.00m);
        _orders[orderNullSafe.Id] = orderNullSafe;
    }

    public static Order? GetOrder(int id)
    {
        return _orders.TryGetValue(id, out var order) ? order : null;
    }

    public static IEnumerable<Order> GetAllOrders()
    {
        return _orders.Values;
    }

    public static ExternalWebhookUser GetSampleWebhookUser() => new()
    {
        first_name = "Eleanor",
        last_name = "Vance",
        user_age = 34,
        email_address = "eleanor.vance@hillhouse.org"
    };

    public static LegacyProduct GetSampleLegacyProduct() => new()
    {
        m_Title = "Enterprise High-Performance Rack Server",
        CategoryModel = "DataCenter Infrastructure",
        m_Price = 4999.00m
    };

    public static WebCustomerSource GetSampleCustomerRecordSource() => new()
    {
        Id = 778,
        FullName = "Marcus Aurelius",
        Tier = "Platinum Executive"
    };

    public static UserProfileEntity GetSampleUserProfileEntity() => new()
    {
        Id = 401,
        Username = "ada.lovelace",
        PasswordHash = "pbkdf2$10000$h6H91kA8x...$TopSecretHash",
        InternalSecurityNotes = "VIP customer; elevated 2FA required.",
        FirstName = "Ada",
        LastName = "Lovelace",
        Street = "12 St. James's Square",
        City = "London",
        PostalCode = "SW1Y 4LB",
        Age = 36,
        Bio = null, // Intentional null to demonstrate Fallback
        UnsanitizedFeedback = "   Super fast library and amazing API design!   \t\n   "
    };

    public static ReverseEmployeeEntity GetSampleReverseEmployeeEntity() => new()
    {
        Id = 502,
        FullName = "Katherine Johnson",
        Department = "Orbital Mechanics",
        ConfidentialCode = "NASA-APOLLO-11"
    };

    public static DirtyTextEntity GetSampleDirtyTextEntity() => new()
    {
        Title = "   Clean Architecture in .NET 9    ",
        Description = "   \t A comprehensive guide with zero excess whitespace.   \n  ",
        Tags = "   csharp, dotnet, automapsharp, performance   "
    };

    public static WebMultiItemOrder GetSampleMultiItemOrder() => new()
    {
        OrderId = 8001,
        CustomerName = "Apollo Space Exploration",
        Items = new List<WebOrderItem>
        {
            new() { Id = 1, ProductName = "Thermal Heat Shield Tile", UnitPrice = 450.00m, Quantity = 4 },
            new() { Id = 2, ProductName = "Cryogenic Fuel Sensor", UnitPrice = 1200.50m, Quantity = 2 },
            new() { Id = 3, ProductName = "Titanium Fastener Kit (100x)", UnitPrice = 85.00m, Quantity = 10 }
        },
        Tags = new HashSet<string> { "Aerospace", "Cryogenic", "Mission-Critical", "Aerospace" }, // Duplicate test
        Specs = new Dictionary<string, string>
        {
            ["Tolerance"] = "0.001mm",
            ["Material"] = "Ti-6Al-4V",
            ["Compliance"] = "MIL-STD-810G"
        }
    };

    public static WebMultiItemOrder GetSampleNullCollectionOrder() => new()
    {
        OrderId = 8002,
        CustomerName = "Orion Satellite Labs",
        Items = null, // Intentional null
        Tags = null,
        Specs = null
    };

    public static WebProductSource GetSampleProductSource() => new()
    {
        Sku = "QUANTUM-SERVER-9000",
        Cost = 3500.00m,
        IsEnterprise = true
    };

    public static WebOrgEnterprise GetSampleHierarchy() => new()
    {
        Name = "Alphabet Inc.",
        Division = new WebOrgDivision
        {
            Name = "Google DeepMind",
            Department = new WebOrgDepartment
            {
                Name = "Advanced Agentic Coding Group",
                Manager = new WebOrgManager
                {
                    Name = "Demis Hassabis",
                    Title = "Chief Executive Officer & Co-Founder",
                    Email = "demis@deepmind.com"
                }
            }
        }
    };

    public static WebCircularEmployee GetSampleCircularEmployeeTree()
    {
        var director = new WebCircularEmployee
        {
            Id = 1,
            Name = "Grace Hopper",
            Role = "Director of Naval Computing"
        };

        var lead = new WebCircularEmployee
        {
            Id = 2,
            Name = "Alan Turing",
            Role = "Principal Systems Architect",
            Manager = director
        };

        var engineer = new WebCircularEmployee
        {
            Id = 3,
            Name = "Ada Lovelace",
            Role = "Lead Algorithm Engineer",
            Manager = lead
        };

        lead.Subordinates.Add(engineer);
        director.Subordinates.Add(lead);

        return director;
    }

    public static WebOrderTaxSource GetSampleOrderTaxSource() => new()
    {
        OrderId = 9101,
        CustomerName = "Silicon Valley Innovations Inc.",
        Subtotal = 12500.00m,
        ShippingState = "CA"
    };

    public static WebRawJsonSource GetSampleRawJsonSource() => new()
    {
        DocumentId = "DOC-CFG-9981",
        JsonPayload = "{\"ClusterName\":\"Production-Kube-US-West\",\"NodeCount\":32,\"Region\":\"us-west-2\",\"HighAvailability\":true}"
    };

    public static List<WebPayment> GetSamplePayments() => new()
    {
        new WebCreditCardPayment
        {
            TransactionId = "TXN-CC-88219",
            Amount = 1499.99m,
            Currency = "USD",
            ProcessedAt = new DateTime(2026, 11, 4, 14, 25, 0, DateTimeKind.Utc),
            CardHolder = "Grace Brewster Hopper",
            MaskedCardNumber = "************4242",
            CardNetwork = "Visa Infinite"
        },
        new WebCryptoPayment
        {
            TransactionId = "TXN-CRYPTO-55102",
            Amount = 4.85m,
            Currency = "ETH",
            ProcessedAt = new DateTime(2026, 11, 4, 15, 10, 0, DateTimeKind.Utc),
            WalletAddress = "0x71C...8976B3921",
            BlockchainNetwork = "Ethereum Mainnet",
            GasFee = 0.0042m
        },
        new WebBankTransferPayment
        {
            TransactionId = "TXN-WIRE-10023",
            Amount = 85000.00m,
            Currency = "USD",
            ProcessedAt = new DateTime(2026, 11, 4, 16, 05, 0, DateTimeKind.Utc),
            BankName = "JPMorgan Chase Bank, N.A.",
            RoutingNumberMasked = "*****0021"
        }
    };

    public static WebAuditedEntity GetSampleAuditedEntity() => new()
    {
        EntityId = 4402,
        EntityName = "Deep Space Autonomous Probe Telemetry",
        ConfidentialCode = "NASA-VOYAGER-SEC-99"
    };
}
