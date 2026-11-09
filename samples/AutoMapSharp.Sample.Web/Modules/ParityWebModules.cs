using AutoMapSharp.Collections;
using AutoMapSharp.Configuration;
using AutoMapSharp.Sample.Web.Models;
using AutoMapSharp.Validation;

namespace AutoMapSharp.Sample.Web.Modules;

/// <summary>
/// Mappings for the Parity demo pages. Code between snippet markers is shown on the pages.
/// </summary>
public class ParityWebModule : MappingModule
{
    public ParityWebModule()
    {
        // <snippet:Conditions>
        DefineMap<Employee, EmployeeCard>()
            // PreCondition runs before the value is read, so a false result skips it entirely.
            .ForProperty(d => d.Nickname, o => o.PreCondition(s => s.Age >= 18).MapSource(s => "Senior " + s.Name))
            // These conditions see the resolved value, and the destination's current value.
            .ForProperty(d => d.Name, o => o.If((s, d, value) => value.Length > 1))
            .ForProperty(d => d.Age, o => o.If((s, d, value, current) => value > current));
        // </snippet:Conditions>

        // <snippet:DestinationValues>
        DefineMap<Preferences, PreferencesView>();
        DefineMap<Profile, ProfileView>()
            .ForProperty(d => d.Preferences, o => o.UseDestinationValue())
            .ForProperty(d => d.Draft, o => o.DoNotUseDestinationValue());
        // </snippet:DestinationValues>

        // <snippet:Constructors>
        DefineMap<Shipment7Source, ShippingLabel>()
            .ForCtorParam("barcode", o => o.MapSource(s => "LBL-" + s.Reference))
            .ForCtorParam("copies", o => o.UseValue(2));

        DefineMap<Employee, LocalizedGreeting>()
            .ConstructUsing((s, ctx) => new LocalizedGreeting((string)ctx.Items["culture"]!));
        // </snippet:Constructors>

        // <snippet:MappingActions>
        // StampExportTime is created from the container, so it can depend on ISampleClock.
        DefineMap<Document, DocumentView>()
            .ForProperty(d => d.ExportedAtUtc, o => o.Skip())
            .AfterMap<StampExportTime>();
        // </snippet:MappingActions>

        // <snippet:Converters>
        DefineMap<Receipt, ReceiptView>()
            .ForProperty(d => d.Total, o => o.ConvertUsing<CentsToMoney>())
            .ForProperty(d => d.Tax, o => o.ConvertUsing<CentsToMoney, long>(s => s.TaxCents))
            .ForProperty(d => d.Memo, o => o.AddTransform(m => m.Trim()).AddTransform(m => m.ToUpperInvariant()));
        // </snippet:Converters>

        // <snippet:Inheritance>
        DefineMap<MediaItem, MediaView>()
            .IncludeAllDerived()
            .ForProperty(d => d.Kind, o => o.MapSource(s => s.GetType().Name));
        DefineMap<Book, BookView>();
        DefineMap<AudioBook, AudioBookView>();
        DefineMap<Film, FilmView>();

        DefineMap<Gadget, GadgetView>().As<GadgetDetailView>();
        DefineMap<Gadget, GadgetDetailView>().ForProperty(d => d.Warranty, o => o.Skip());
        // </snippet:Inheritance>

        // <snippet:OpenGenerics>
        DefineMap(typeof(PagedResult<>), typeof(PageView<>))
            .ForProperty("Total", o => o.MapSource("TotalCount"))
            .ForProperty("Page", o => o.MapSource("PageNumber"));
        // </snippet:OpenGenerics>
    }
}

/// <summary>
/// Module-wide member filters for the Fields and Filters page.
/// </summary>
public class ParityFiltersWebModule : MappingModule
{
    public ParityFiltersWebModule()
    {
        // <snippet:FieldsAndFilters>
        // Public fields are mapped like properties. These filters apply to the whole module.
        ShouldMapMethod = m => !m.Name.Contains("Secret");
        ShouldMapProperty = p => p.Name != "InternalNotes";

        // The method filter leaves SecretKey without a source, and validation reports that,
        // so it is marked as skipped on purpose. InternalNotes is filtered out on both sides.
        DefineMap<SensorReading, SensorView>()
            .ForProperty(d => d.SecretKey, o => o.Skip());
        // </snippet:FieldsAndFilters>
    }
}

/// <summary>
/// Convention settings for the Naming Conventions page.
/// </summary>
public class ParityConventionsWebModule : MappingModule
{
    public ParityConventionsWebModule()
    {
        // <snippet:NamingConventions>
        RecognizeDestinationPrefixes("Dto"); // DtoName matches Name
        ReplaceMemberName("ß", "ss");        // Straße matches Strasse
        // GetFullName() matches FullName through the default "Get" prefix.
        // emailAddress (camelCase) matches EmailAddress without configuration.
        DefineMap<LegacyCustomerRow, CustomerRowView>();
        // </snippet:NamingConventions>
    }
}

/// <summary>
/// Inline configuration for the Inline Configuration page.
/// </summary>
public static class InlineConfigurationDemo
{
    public static MappingConfiguration Build()
    {
        // <snippet:InlineConfiguration>
        var config = new MappingConfiguration(cfg =>
        {
            cfg.ValueTransformers.Add<string>(s => s?.Trim()!);
            cfg.DefineMap<Employee, EmployeeCard>()
                .ForProperty(d => d.Nickname, o => o.UseValue("defined inline"));
        });

        config.AssertConfigurationIsValid();
        // </snippet:InlineConfiguration>
        return config;
    }
}

/// <summary>
/// A deliberately broken configuration for the Validation page. The module is a private nested class,
/// so assembly scanning does not register it with the application's engine.
/// </summary>
public static class ValidationDemo
{
    public class Wide { public string Name { get; set; } = ""; public string Extra { get; set; } = ""; }
    public class Narrow { public string Name { get; set; } = ""; }
    public class Holder { public Part Part { get; set; } = new(); public int Code { get; set; } }
    public class Part { public string Label { get; set; } = ""; }
    public class HolderView { public PartView? Part { get; set; } public DateTime Code { get; set; } }
    public class PartView { public string Label { get; set; } = ""; public string Missing { get; set; } = ""; }
    public class Locked { public Locked(string secretToken) { SecretToken = secretToken; } public string SecretToken { get; } }

    public static ConfigurationValidationResult Run() => new MapEngine(new Module()).ValidateConfiguration();

    private sealed class Module : MappingModule
    {
        public Module()
        {
            // The analyzer flags the broken Holder map at build time too. It is suppressed here on purpose.
#pragma warning disable AMS002
            // <snippet:Validation>
            DefineMap<Wide, Narrow>().ValidateMembers(MemberList.Source); // Extra is never used
            DefineMap<Holder, HolderView>();                              // Code: int -> DateTime, Part.Missing has no source
            DefineMap<Narrow, Locked>();                                  // secretToken cannot be resolved
            // </snippet:Validation>
#pragma warning restore AMS002
        }
    }
}

/// <summary>
/// Mappings for the second set of Parity pages.
/// </summary>
public class ParityTier2WebModule : MappingModule
{
    public ParityTier2WebModule()
    {
        // <snippet:Projections>
        // Fallback becomes COALESCE in SQL. Items is only selected when it is expanded.
        DefineMap<DbOrder, OrderCardDto>()
            .ForProperty(d => d.CustomerEmail, o => o.MapSource(s => s.Customer!.Email).Fallback("(no email)"))
            .ForProperty(d => d.Items, o => o.ExplicitExpansion());

        // Include pairs become type checks, which EF Core translates for TPH inheritance.
        DefineMap<CatalogEntry, CatalogEntryView>().Include<Course, CourseView>().Include<Workshop, WorkshopView>();
        DefineMap<Course, CourseView>();
        DefineMap<Workshop, WorkshopView>();
        // </snippet:Projections>

        // <snippet:ExtensionMethods>
        IncludeSourceExtensionMethods(typeof(MemberExtensions));
        // FullName <- GetFullName(), VisitCount <- VisitCount(), LastVisit <- LastVisit()
        DefineMap<Member, MemberBadge>();
        // </snippet:ExtensionMethods>
    }
}

/// <summary>
/// Collection merging for the Collection Merge page.
/// </summary>
public class ParityCollectionsWebModule : MappingModule
{
    public ParityCollectionsWebModule()
    {
        // <snippet:CollectionMerge>
        // From the AutoMapSharp.Collections package.
        DefineMap<CartLineDto, CartLine>()
            .EqualityComparison((dto, line) => dto.Sku == line.Sku)
            .ForProperty(d => d.TrackingId, o => o.Skip());
        DefineMap<CartDto, Cart>();
        // </snippet:CollectionMerge>
    }
}
