using System.Globalization;

namespace AutoMapSharp.Sample.Web.Models;

// ---- Shared ----

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string? Nickname { get; set; }
}

public class EmployeeCard
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string? Nickname { get; set; }
}

// ---- 13.2 Fields and filters ----

public class SensorReading
{
    public string DeviceId = "TEMP-07";
    public double Celsius = 21.5;
    public string GetLocation() => "Server room";
    public string GetSecretKey() => "do-not-copy";
    public string InternalNotes { get; set; } = "calibrated 2026-10-01";
}

public class SensorView
{
    public string DeviceId = "";
    public double Celsius;
    public string? Location { get; set; }
    public string? SecretKey { get; set; }
    public string? InternalNotes { get; set; }
}

// ---- 13.5 Destination values ----

public class Preferences { public string Theme { get; set; } = ""; public string Language { get; set; } = ""; }
public class PreferencesView { public string Theme { get; set; } = ""; public string Language { get; set; } = ""; }
public class Profile { public string Name { get; set; } = ""; public Preferences? Preferences { get; set; } public Preferences? Draft { get; set; } }
public class ProfileView { public string Name { get; set; } = ""; public PreferencesView? Preferences { get; set; } public PreferencesView? Draft { get; set; } }

// ---- 13.7 Constructors ----

public class Shipment7Source { public string Reference { get; set; } = ""; public string Carrier { get; set; } = ""; }

public class ShippingLabel
{
    public ShippingLabel(string barcode, string carrier, int copies)
    {
        Barcode = barcode;
        Carrier = carrier;
        Copies = copies;
    }

    public string Barcode { get; }
    public string Carrier { get; }
    public int Copies { get; }
}

public class LocalizedGreeting { public LocalizedGreeting(string culture) { Culture = culture; } public string Culture { get; } public string Name { get; set; } = ""; }

// ---- 13.8 Mapping actions ----

public interface ISampleClock { DateTime UtcNow { get; } }
public class FixedSampleClock : ISampleClock { public DateTime UtcNow => new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc); }

public class Document { public string Title { get; set; } = ""; }
public class DocumentView { public string Title { get; set; } = ""; public DateTime ExportedAtUtc { get; set; } }

public class StampExportTime : IMappingAction<Document, DocumentView>
{
    private readonly ISampleClock _clock;
    public StampExportTime(ISampleClock clock) { _clock = clock; }
    public void Process(Document source, DocumentView destination, ResolutionContext context) => destination.ExportedAtUtc = _clock.UtcNow;
}

// ---- 13.9 / 13.10 Value converters and transforms ----

public class CentsToMoney : IValueConverter<long, string>
{
    public string Convert(long sourceMember, ResolutionContext context) =>
        (sourceMember / 100m).ToString("0.00", CultureInfo.InvariantCulture) + " " + (context.Items.TryGetValue("currency", out var c) ? c : "USD");
}

public class Receipt { public long Total { get; set; } public long TaxCents { get; set; } public string Memo { get; set; } = ""; }
public class ReceiptView { public string Total { get; set; } = ""; public string Tax { get; set; } = ""; public string Memo { get; set; } = ""; }

// ---- 13.11 Inheritance ----

public abstract class MediaItem { public string Title { get; set; } = ""; public string Kind { get; set; } = ""; }
public class Book : MediaItem { public int Pages { get; set; } }
public class AudioBook : Book { public int Minutes { get; set; } }
public class Film : MediaItem { public int Runtime { get; set; } }
public abstract class MediaView { public string Title { get; set; } = ""; public string Kind { get; set; } = ""; }
public class BookView : MediaView { public int Pages { get; set; } }
public class AudioBookView : BookView { public int Minutes { get; set; } }
public class FilmView : MediaView { public int Runtime { get; set; } }

public class Gadget { public string Name { get; set; } = ""; }
public class GadgetView { public string Name { get; set; } = ""; }
public class GadgetDetailView : GadgetView { public string Warranty { get; set; } = "2 years"; }

// ---- 13.12 Open generics ----

public class PagedResult<T> { public List<T> Items { get; set; } = new(); public int TotalCount { get; set; } public int PageNumber { get; set; } }
public class PageView<T> { public List<T> Items { get; set; } = new(); public int Total { get; set; } public int Page { get; set; } }

// ---- 13.13 Naming conventions ----

public class LegacyCustomerRow
{
    public string GetFullName() => "Ada Lovelace";
    public string Straße { get; set; } = "Main Street 1";
    public string emailAddress { get; set; } = "ada@example.com";
    public string Name { get; set; } = "ada";
}

public class CustomerRowView
{
    public string? FullName { get; set; }
    public string? Strasse { get; set; }
    public string? EmailAddress { get; set; }
    public string? DtoName { get; set; }
}

// ---- Parity matrix ----

public class ParityRow
{
    public string Area { get; set; } = "";
    public string AutoMapper { get; set; } = "";
    public string AutoMapSharp { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
}

// ---- 14.1 / 14.2 Projections ----

public class OrderCardDto
{
    public string OrderNumber { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public List<OrderItemProjectionDto>? Items { get; set; }
}

public abstract class CatalogEntry { public int Id { get; set; } public string Title { get; set; } = ""; }
public class Course : CatalogEntry { public int Hours { get; set; } }
public class Workshop : CatalogEntry { public string City { get; set; } = ""; }
public abstract class CatalogEntryView { public int Id { get; set; } public string Title { get; set; } = ""; }
public class CourseView : CatalogEntryView { public int Hours { get; set; } }
public class WorkshopView : CatalogEntryView { public string City { get; set; } = ""; }

// ---- 14.3 Dictionaries ----

public class FormSubmission
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public bool Subscribe { get; set; }
    public PostalAddress? Address { get; set; }
}

// ---- 14.4 Extension methods ----

public class Member { public string First { get; set; } = ""; public string Last { get; set; } = ""; public List<DateTime> Visits { get; set; } = new(); }
public class MemberBadge { public string FullName { get; set; } = ""; public int VisitCount { get; set; } public DateTime? LastVisit { get; set; } }

public static class MemberExtensions
{
    public static string GetFullName(this Member member) => member.First + " " + member.Last;
    public static int VisitCount(this Member member) => member.Visits.Count;
    public static DateTime? LastVisit(this Member member) => member.Visits.Count == 0 ? null : member.Visits.Max();
}

// ---- 14.5 Attributes ----

public class SupportTicket { public int Number { get; set; } public string Subject { get; set; } = ""; public string AssignedAgent { get; set; } = ""; public string InternalNotes { get; set; } = ""; }

[MapFrom(typeof(SupportTicket), ReverseMap = true)]
public class SupportTicketView
{
    public int Number { get; set; }
    public string Subject { get; set; } = "";

    [SourceMember(nameof(SupportTicket.AssignedAgent))]
    public string Owner { get; set; } = "";

    [SkipMap]
    public string? InternalNotes { get; set; }
}

// ---- 14.7 Collection merging ----

public class CartLine { public string Sku { get; set; } = ""; public int Quantity { get; set; } public Guid TrackingId { get; set; } = Guid.NewGuid(); }
public class Cart { public List<CartLine> Lines { get; set; } = new(); }
public class CartLineDto { public string Sku { get; set; } = ""; public int Quantity { get; set; } }
public class CartDto { public List<CartLineDto> Lines { get; set; } = new(); }
