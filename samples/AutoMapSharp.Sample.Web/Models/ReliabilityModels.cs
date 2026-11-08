using System.Text.Json;

namespace AutoMapSharp.Sample.Web.Models;

/// <summary>
/// Data for the shared demo layout used by the Reliability &amp; Parity pages.
/// </summary>
public class FeatureDemoViewModel
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string FixedIn { get; set; } = "";
    public string SourceJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public string ModuleSnippet { get; set; } = "";
    public string AutoMapperSnippet { get; set; } = "";
    public string DocPath { get; set; } = "";
    public List<string> Notes { get; set; } = new();
    public string? FormAction { get; set; }
    public List<DemoField> FormFields { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ToJson(object? value) => JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), JsonOptions);
}

public class DemoField
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public enum ShipmentPriority { Low, Normal, Urgent }

public readonly struct Weight
{
    public Weight(decimal kilograms) { Kilograms = kilograms; }
    public decimal Kilograms { get; }
}

public class ShipmentRecord
{
    public int Id { get; set; }
    public int? CustomerRating { get; set; }
    public decimal? DeclaredValue { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public Guid? TrackingId { get; set; }
    public bool? SignatureRequired { get; set; }
    public ShipmentPriority? Priority { get; set; }
    public TimeSpan TransitTime { get; set; }
    public DateOnly ShipDate { get; set; }
    public Weight Weight { get; set; }
    public object? Metadata { get; set; }
}

public class ShipmentViewModel
{
    public int Id { get; set; }
    public int? CustomerRating { get; set; }
    public decimal? DeclaredValue { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public Guid? TrackingId { get; set; }
    public bool? SignatureRequired { get; set; }
    public ShipmentPriority? Priority { get; set; }
    public TimeSpan TransitTime { get; set; }
    public DateOnly ShipDate { get; set; }
    public Weight Weight { get; set; }
    public object? Metadata { get; set; }
}

public class BulkOrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int BulkLineCount { get; set; }
}

public class SafeParametersViewModel
{
    public int SelectedMinQuantity { get; set; }
    public int ComparisonMinQuantity { get; set; }
    public List<BulkOrderSummaryDto> SelectedResults { get; set; } = new();
    public List<BulkOrderSummaryDto> ComparisonResults { get; set; } = new();
    public string GeneratedSql { get; set; } = "";
    public string ModuleSnippet { get; set; } = "";
    public string QuerySnippet { get; set; } = "";
}

// ---- Records with nested types (finding F09) ----

public class CustomerEntity { public string Name { get; set; } = ""; }
public class AddressEntity { public string City { get; set; } = ""; public string Country { get; set; } = ""; }
public class OrderLineEntity { public string Sku { get; set; } = ""; public int Quantity { get; set; } }

public class PurchaseOrderEntity
{
    public int Id { get; set; }
    public CustomerEntity Customer { get; set; } = new();
    public AddressEntity ShippingAddress { get; set; } = new();
    public AddressEntity? BillingAddress { get; set; }
    public List<OrderLineEntity> Lines { get; set; } = new();
}

public record AddressRecord(string City, string Country);
public record OrderLineRecord(string Sku, int Quantity);
public record PurchaseOrderRecord(int Id, string CustomerName, AddressRecord ShippingAddress, AddressRecord? BillingAddress, IReadOnlyList<OrderLineRecord> Lines);

// ---- ReverseMap unflattening (findings F10 and F11) ----

public class ContactInfo { public string Email { get; set; } = ""; public string Phone { get; set; } = ""; }
public class PostalAddress { public string City { get; set; } = ""; }

public class CustomerAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ContactInfo? Contact { get; set; }
    public PostalAddress? Address { get; set; }
}

public class CustomerAccountForm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string AddressCity { get; set; } = "";
}

// ---- Enum mapping by name (finding F12) ----

public enum LegacyPriority { Low, Medium, High, Critical, Escalated }
public enum TicketPriority { Critical, High, Medium, Low, Unknown }

public class LegacyTicket { public string Title { get; set; } = ""; public LegacyPriority Priority { get; set; } }
public class TicketView { public string Title { get; set; } = ""; public TicketPriority Priority { get; set; } }

// ---- Collection shapes (finding F13) ----

public class TagSource
{
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, int> Scores { get; set; } = new();
}

public class CollectionShapesView
{
    public System.Collections.ObjectModel.ObservableCollection<string> Observable { get; set; } = new();
    public System.Collections.ObjectModel.ReadOnlyCollection<string> ReadOnly { get; set; } = new(new List<string>());
    public System.Collections.Immutable.ImmutableArray<string> Immutable { get; set; }
    public SortedSet<string> Sorted { get; set; } = new();
    public Queue<string> Queue { get; set; } = new();
    public IReadOnlySet<string> Set { get; set; } = new HashSet<string>();
    public LinkedList<string> Linked { get; set; } = new();
    public SortedDictionary<string, int> SortedScores { get; set; } = new();
}

// ---- Shared references (finding F08) ----

public class TeamMember { public string Name { get; set; } = ""; public string Role { get; set; } = ""; }

public class Team
{
    public string Name { get; set; } = "";
    public TeamMember? Lead { get; set; }
    public List<TeamMember> Members { get; set; } = new();
    public TeamMember? Spotlight { get; set; }
}

public class TeamMemberDto { public string Name { get; set; } = ""; public string Role { get; set; } = ""; }
public class TeamMemberCard { public string Name { get; set; } = ""; }

public class TeamDto
{
    public string Name { get; set; } = "";
    public TeamMemberDto? Lead { get; set; }
    public List<TeamMemberDto> Members { get; set; } = new();
    public TeamMemberCard? Spotlight { get; set; }
}

// ---- Scoped services in resolvers (findings F19 and F20) ----

/// <summary>
/// A scoped service: one instance per HTTP request.
/// </summary>
public class RequestTrace
{
    public Guid RequestId { get; } = Guid.NewGuid();
}

public class AuditEntry { public string Action { get; set; } = ""; }
public class AuditEntryView { public string Action { get; set; } = ""; public Guid RequestId { get; set; } }

/// <summary>
/// Reads the scoped <see cref="RequestTrace"/>. It is not registered by hand: assembly scanning registers it,
/// and the engine creates it from the current request's scope.
/// </summary>
public class RequestIdResolver : IPropertyResolver<AuditEntry, AuditEntryView, Guid>
{
    private readonly RequestTrace _trace;
    public RequestIdResolver(RequestTrace trace) { _trace = trace; }
    public Guid Resolve(AuditEntry source, AuditEntryView destination, ResolutionContext context) => _trace.RequestId;
}
