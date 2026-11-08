using AutoMapSharp.Sample.Web.Infrastructure;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

/// <summary>
/// Demo pages for the fixes listed in KNOWN_ISSUES.md. Each page shows the source, the mapped result,
/// the exact module code that produced it, and the AutoMapper equivalent.
/// </summary>
public class ReliabilityController : Controller
{
    private readonly IMapEngine _engine;

    public ReliabilityController(IMapEngine engine)
    {
        _engine = engine;
    }

    public static ShipmentRecord CreateSampleShipment() => new()
    {
        Id = 4021,
        CustomerRating = 4,
        DeclaredValue = 1299.99m,
        DeliveredAtUtc = new DateTime(2026, 11, 5, 16, 42, 0, DateTimeKind.Utc),
        TrackingId = new Guid("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
        SignatureRequired = true,
        Priority = ShipmentPriority.Urgent,
        TransitTime = TimeSpan.FromHours(38.5),
        ShipDate = new DateOnly(2026, 11, 3),
        Weight = new Weight(12.75m),
        Metadata = new Dictionary<string, string> { ["carrier"] = "NordExpress", ["dock"] = "B4" }
    };

    // Findings F01, F02 and F07
    public IActionResult NullableValues()
    {
        var source = CreateSampleShipment();
        var result = _engine.Map<ShipmentRecord, ShipmentViewModel>(source);

        var model = new FeatureDemoViewModel
        {
            Title = "Nullable Values, Structs & Object Properties",
            Summary = "Nullable value types, structs such as TimeSpan and DateOnly, and object-typed properties keep their values. No configuration is needed.",
            FixedIn = "Fixed during the code review (findings F01, F02 and F07). Before the fix, every value below on the right came out as 0, null-equivalent, or an empty object.",
            SourceJson = FeatureDemoViewModel.ToJson(source),
            ResultJson = FeatureDemoViewModel.ToJson(result),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "NullableValues"),
            AutoMapperSnippet = "CreateMap<ShipmentRecord, ShipmentViewModel>();",
            DocPath = "docs/features/type-conversions.md",
            Notes = new List<string>
            {
                "A value that is already the right type is assigned as is, the same way AutoMapper does it.",
                "To copy a nested object instead of sharing the reference, define a map for that type, for example DefineMap<Address, Address>().",
                "The Metadata dictionary on the right is the same instance as on the left."
            }
        };

        ViewBag.SameMetadataInstance = ReferenceEquals(source.Metadata, result.Metadata);
        return Demo(model);
    }

    // Finding F09
    public IActionResult RecordsNested()
    {
        var source = new PurchaseOrderEntity
        {
            Id = 5012,
            Customer = new CustomerEntity { Name = "Grace Hopper" },
            ShippingAddress = new AddressEntity { City = "Arlington", Country = "US" },
            BillingAddress = null,
            Lines = { new() { Sku = "COBOL-101", Quantity = 2 }, new() { Sku = "UNIVAC-I", Quantity = 1 } }
        };

        var result = _engine.Map<PurchaseOrderEntity, PurchaseOrderRecord>(source);

        return Demo(new FeatureDemoViewModel
        {
            Title = "Records with Nested Types",
            Summary = "Positional records get nested records, collections and flattened values in their constructor parameters.",
            FixedIn = "Fixed during the code review (finding F09). Before the fix, this map threw, because constructor parameters received the unmapped source objects.",
            SourceJson = FeatureDemoViewModel.ToJson(source),
            ResultJson = FeatureDemoViewModel.ToJson(result),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "RecordsNested"),
            AutoMapperSnippet = "CreateMap<AddressEntity, AddressRecord>();\nCreateMap<OrderLineEntity, OrderLineRecord>();\nCreateMap<PurchaseOrderEntity, PurchaseOrderRecord>();",
            DocPath = "docs/features/constructor-mapping-and-records.md",
            Notes = new List<string>
            {
                "The engine picks the constructor with the most parameters that can all be resolved.",
                "Members set through the constructor are not set again afterwards.",
                "A null nested value, like BillingAddress here, stays null."
            }
        });
    }

    // Findings F10 and F11
    public IActionResult ReverseUnflatten(string name = "Margaret Hamilton", string contactEmail = "margaret@apollo.example", string contactPhone = "+1 555 0100", string addressCity = "Cambridge")
    {
        var form = new CustomerAccountForm { Id = 7, Name = name, ContactEmail = contactEmail, ContactPhone = contactPhone, AddressCity = addressCity };
        var entity = _engine.Map<CustomerAccountForm, CustomerAccount>(form);

        return Demo(new FeatureDemoViewModel
        {
            Title = "ReverseMap Unflattening",
            Summary = "A flat edit form maps back into the nested entity. ReverseMap creates the nested objects when they are missing.",
            FixedIn = "Fixed during the code review (findings F10 and F11). Before the fix, reverse maps could write a flattened value into the wrong root member.",
            SourceJson = FeatureDemoViewModel.ToJson(form),
            ResultJson = FeatureDemoViewModel.ToJson(entity),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "ReverseUnflatten"),
            AutoMapperSnippet = "CreateMap<CustomerAccount, CustomerAccountForm>().ReverseMap();",
            DocPath = "docs/features/forpath-and-unflattening.md",
            FormAction = "/Reliability/ReverseUnflatten",
            FormFields = new List<DemoField>
            {
                new() { Name = "name", Label = "Name", Value = name },
                new() { Name = "contactEmail", Label = "ContactEmail", Value = contactEmail },
                new() { Name = "contactPhone", Label = "ContactPhone", Value = contactPhone },
                new() { Name = "addressCity", Label = "AddressCity", Value = addressCity }
            },
            Notes = new List<string>
            {
                "Edit the form fields and press Map it to map the form back into a CustomerAccount.",
                "For a nested member that does not follow the naming convention, use ForPath(d => d.Contact.Email, o => o.MapSource(s => s.Email)).",
                "ForProperty only accepts top-level members and points you to ForPath for nested ones."
            }
        });
    }

    // Finding F12
    public IActionResult EnumByName()
    {
        var source = Enum.GetValues<LegacyPriority>().Select(p => new LegacyTicket { Title = $"{p} ticket", Priority = p }).ToList();
        var result = _engine.Map<List<LegacyTicket>, List<TicketView>>(source);

        return Demo(new FeatureDemoViewModel
        {
            Title = "Enum Mapping by Name",
            Summary = "Enum values are matched by name, so two enums with the same names in a different order map correctly.",
            FixedIn = "Fixed during the code review (finding F12). Before the fix, enums were matched by numeric value, so Low became Critical.",
            SourceJson = string.Join("\n", source.Select(t => $"{t.Title}: {t.Priority} ({(int)t.Priority})")),
            ResultJson = string.Join("\n", result.Select(t => $"{t.Title}: {t.Priority} ({(int)t.Priority})")),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "EnumByName"),
            AutoMapperSnippet = "CreateMap<LegacyTicket, TicketView>();",
            DocPath = "docs/features/enum-mapping.md",
            Notes = new List<string>
            {
                "LegacyPriority.Escalated has no matching name, so its numeric value 4 is used, which is TicketPriority.Unknown.",
                "Names are compared ignoring case. Flag combinations fall back to the numeric value."
            }
        });
    }

    // Finding F13
    public IActionResult CollectionShapes()
    {
        var source = new TagSource
        {
            Tags = new List<string> { "mapping", "dotnet", "records", "dotnet" },
            Scores = new Dictionary<string, int> { ["speed"] = 9, ["accuracy"] = 10 }
        };
        var result = _engine.Map<TagSource, CollectionShapesView>(source);

        return Demo(new FeatureDemoViewModel
        {
            Title = "Every Collection Shape",
            Summary = "One source list fills observable, read-only, immutable, sorted, queue, set and linked-list destinations.",
            FixedIn = "Fixed during the code review (finding F13). Before the fix, every destination below except a plain List or array threw.",
            SourceJson = FeatureDemoViewModel.ToJson(source),
            ResultJson = FeatureDemoViewModel.ToJson(result),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "CollectionShapes"),
            AutoMapperSnippet = "CreateMap<TagSource, CollectionShapesView>()\n    .ForMember(d => d.Observable, o => o.MapFrom(s => s.Tags))\n    // ...one ForMember per destination",
            DocPath = "docs/features/collections-mapping.md",
            Notes = new List<string>
            {
                "Sets drop the duplicate \"dotnet\". SortedSet and SortedDictionary sort their items.",
                "When the destination already holds a mutable collection, it is cleared and refilled, so its identity is kept."
            }
        });
    }

    // Finding F08
    public IActionResult SharedReferences()
    {
        var lead = new TeamMember { Name = "Barbara Liskov", Role = "Lead" };
        var source = new Team
        {
            Name = "Abstractions",
            Lead = lead,
            Members = { lead, new TeamMember { Name = "John Guttag", Role = "Engineer" } },
            Spotlight = lead
        };

        var result = _engine.Map<Team, TeamDto>(source);
        var leadIsShared = ReferenceEquals(result.Lead, result.Members[0]);

        ViewBag.LeadIsShared = leadIsShared;
        return Demo(new FeatureDemoViewModel
        {
            Title = "Shared References Across Destination Types",
            Summary = "With PreserveReferences, one source object maps to one destination object per destination type.",
            FixedIn = "Fixed during the code review (finding F08). Before the fix, this map threw, because the lead was already mapped to TeamMemberDto when Spotlight asked for a TeamMemberCard.",
            SourceJson = FeatureDemoViewModel.ToJson(source),
            ResultJson = FeatureDemoViewModel.ToJson(result),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "SharedReferences"),
            AutoMapperSnippet = "CreateMap<Team, TeamDto>().PreserveReferences();\nCreateMap<TeamMember, TeamMemberDto>().PreserveReferences();\nCreateMap<TeamMember, TeamMemberCard>().PreserveReferences();",
            DocPath = "docs/features/circular-references-and-depth-limits.md",
            Notes = new List<string>
            {
                $"Lead and Members[0] are the same TeamMemberDto instance: {leadIsShared}.",
                "Spotlight is a separate TeamMemberCard, because it is a different destination type."
            }
        });
    }

    // Findings F19 and F20
    public IActionResult ScopedResolver()
    {
        var source = new List<AuditEntry> { new() { Action = "Opened invoice" }, new() { Action = "Approved invoice" } };
        var result = _engine.Map<List<AuditEntry>, List<AuditEntryView>>(source);

        return Demo(new FeatureDemoViewModel
        {
            Title = "Scoped Services in Resolvers",
            Summary = "A resolver can depend on a scoped service. Both entries below show this request's ID. Reload the page to get a new one.",
            FixedIn = "Fixed during the code review (findings F19 and F20). Before the fix, the engine was a singleton holding the root service provider, so a scoped dependency failed or was shared by every request.",
            SourceJson = FeatureDemoViewModel.ToJson(source),
            ResultJson = FeatureDemoViewModel.ToJson(result),
            ModuleSnippet = SnippetReader.Read("ReliabilityWebModules.cs", "ScopedResolver") +
                "\n\n// Program.cs\nbuilder.Services.AddScoped<RequestTrace>();\nbuilder.Services.AddAutoMapSharp(typeof(Program).Assembly);",
            AutoMapperSnippet = "CreateMap<AuditEntry, AuditEntryView>()\n    .ForMember(d => d.RequestId, o => o.MapFrom<RequestIdResolver>());\n\nservices.AddAutoMapper(typeof(Program));",
            DocPath = "docs/features/dependency-injection.md",
            Notes = new List<string>
            {
                "The compiled configuration is a singleton. Each IMapEngine is a cheap per-scope view of it.",
                "Assembly scanning registers resolvers and converters, including internal ones. A resolver that is not registered still gets its constructor dependencies from the container."
            }
        });
    }

    private IActionResult Demo(FeatureDemoViewModel model) => View("~/Views/Shared/_FeatureDemo.cshtml", model);
}
