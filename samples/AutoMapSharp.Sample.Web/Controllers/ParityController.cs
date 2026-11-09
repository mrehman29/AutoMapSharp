using System.Text.RegularExpressions;
using AutoMapSharp.Sample.Web.Infrastructure;
using AutoMapSharp.Sample.Web.Models;
using AutoMapSharp.Sample.Web.Modules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMapSharp.Sample.Web.Controllers;

/// <summary>
/// One page per AutoMapper parity feature, plus the parity matrix.
/// </summary>
public class ParityController : Controller
{
    private const string ModulesFile = "ParityWebModules.cs";
    private readonly IMapEngine _engine;

    public ParityController(IMapEngine engine)
    {
        _engine = engine;
    }

    // 13.1
    public IActionResult InlineConfiguration()
    {
        var engine = InlineConfigurationDemo.Build().CreateEngine();
        var source = new Employee { Id = 1, Name = "  Grace Hopper  ", Age = 85 };
        var result = engine.Map<Employee, EmployeeCard>(source);

        return Demo("Inline Configuration", "Define maps without writing a module class, like AutoMapper's MapperConfiguration.",
            source, result, "InlineConfiguration",
            "var config = new MapperConfiguration(cfg =>\n{\n    cfg.ValueTransformers.Add<string>(s => s?.Trim());\n    cfg.CreateMap<Employee, EmployeeCard>()\n        .ForMember(d => d.Nickname, o => o.MapFrom(_ => \"defined inline\"));\n});",
            "docs/features/inline-configuration.md",
            "The name on the right is trimmed by the inline value transformer.",
            "new MapEngine(cfg => ...) and AddAutoMapSharp(o => o.AddMaps(cfg => ...)) take the same configuration.");
    }

    // 13.2
    public IActionResult FieldsAndFilters()
    {
        var source = new SensorReading();
        var result = _engine.Map<SensorReading, SensorView>(source);

        return Demo("Fields & Member Filters", "Public fields map like properties. ShouldMapProperty, ShouldMapField and ShouldMapMethod limit which members take part.",
            new { source.DeviceId, source.Celsius, Location = source.GetLocation(), SecretKey = source.GetSecretKey(), source.InternalNotes },
            new { result.DeviceId, result.Celsius, result.Location, result.SecretKey, result.InternalNotes },
            "FieldsAndFilters",
            "public class SensorProfile : Profile\n{\n    public SensorProfile()\n    {\n        ShouldMapMethod = m => !m.Name.Contains(\"Secret\");\n        ShouldMapProperty = p => p.Name != \"InternalNotes\";\n        CreateMap<SensorReading, SensorView>();\n    }\n}",
            "docs/features/property-matching-conventions.md",
            "DeviceId and Celsius are public fields on both sides.",
            "InternalNotes is filtered out on both sides. GetSecretKey() is filtered out as a source, and validation would report SecretKey as unmapped, so it is also marked Skip().");
    }

    // 13.3 and 13.4
    public IActionResult Conditions()
    {
        var existing = new EmployeeCard { Id = 3, Name = "Kept", Age = 50 };
        var source = new Employee { Id = 3, Name = "X", Age = 40 };
        _engine.Map(source, existing);
        var minor = _engine.Map<Employee, EmployeeCard>(new Employee { Id = 4, Name = "Tim", Age = 15 });

        return Demo("Conditions & PreConditions", "PreCondition runs before the value is read. If overloads can see the resolved value and the destination's current value.",
            new { Source = source, ExistingBefore = new { Name = "Kept", Age = 50 }, Minor = new Employee { Id = 4, Name = "Tim", Age = 15 } },
            new { ExistingAfter = existing, MinorResult = minor },
            "Conditions",
            "CreateMap<Employee, EmployeeCard>()\n    .ForMember(d => d.Nickname, o => { o.PreCondition(s => s.Age >= 18); o.MapFrom(s => \"Senior \" + s.Name); })\n    .ForMember(d => d.Name, o => o.Condition((s, d, value) => value.Length > 1))\n    .ForMember(d => d.Age, o => o.Condition((s, d, value, current) => value > current));",
            "docs/features/conditional-mapping-and-fallbacks.md",
            "\"X\" is too short, so the existing name stays. 40 is not greater than 50, so the existing age stays.",
            "Tim is 15, so the PreCondition fails and Nickname is never computed.");
    }

    // 13.5
    public IActionResult DestinationValues()
    {
        var keptPreferences = new PreferencesView { Theme = "light", Language = "en" };
        var oldDraft = new PreferencesView { Theme = "old", Language = "old" };
        var target = new ProfileView { Name = "Ann", Preferences = keptPreferences, Draft = oldDraft };
        var source = new Profile { Name = "Ann", Preferences = new Preferences { Theme = "dark", Language = "nb" }, Draft = new Preferences { Theme = "draft", Language = "nb" } };
        _engine.Map(source, target);

        return Demo("UseDestinationValue", "Choose per member whether an existing destination object is updated in place or replaced.",
            source, new { target, PreferencesKeptSameInstance = ReferenceEquals(keptPreferences, target.Preferences), DraftReplaced = !ReferenceEquals(oldDraft, target.Draft) },
            "DestinationValues",
            "CreateMap<Profile, ProfileView>()\n    .ForMember(d => d.Preferences, o => o.UseDestinationValue())\n    .ForMember(d => d.Draft, o => o.DoNotUseDestinationValue());",
            "docs/features/existing-collection-updates.md",
            "With UseDestinationValue the existing object is updated in place, and kept when the source value is null.",
            "With DoNotUseDestinationValue a new object is always created.");
    }

    // 13.6
    public IActionResult PerCallOptions(string currency = "EUR")
    {
        var log = new List<string>();
        var source = new Receipt { Total = 4999, TaxCents = 1000, Memo = "  thanks  " };
        var result = _engine.Map<Receipt, ReceiptView>(source, opt =>
        {
            opt.Items["currency"] = currency;
            opt.BeforeMap((s, d) => log.Add("BeforeMap: destination created, Total is empty"));
            opt.AfterMap((s, d) => log.Add($"AfterMap: Total is {d.Total}"));
        });

        var model = DemoModel("Per-Call Options", "Pass Items, BeforeMap and AfterMap to a single Map call.",
            source, new { result, CallbackLog = log }, "Converters",
            "var view = mapper.Map<Receipt, ReceiptView>(receipt, opt =>\n{\n    opt.Items[\"currency\"] = \"EUR\";\n    opt.BeforeMap((s, d) => ...);\n    opt.AfterMap((s, d) => ...);\n});",
            "docs/features/per-call-options.md",
            "The CentsToMoney value converter reads the currency from context.Items.",
            "Change the currency with the form above.");
        model.ModuleSnippet = "var view = engine.Map<Receipt, ReceiptView>(receipt, opt =>\n{\n    opt.Items[\"currency\"] = currency;\n    opt.BeforeMap((s, d) => log.Add(...));\n    opt.AfterMap((s, d) => log.Add(...));\n});\n\n// Module\n" + model.ModuleSnippet;
        model.FormAction = "/Parity/PerCallOptions";
        model.FormFields = new List<DemoField> { new() { Name = "currency", Label = "Currency item", Value = currency } };
        return View("~/Views/Shared/_FeatureDemo.cshtml", model);
    }

    // 13.7
    public IActionResult Constructors()
    {
        var source = new Shipment7Source { Reference = "88213", Carrier = "NordExpress" };
        var label = _engine.Map<Shipment7Source, ShippingLabel>(source);
        var greeting = _engine.Map<Employee, LocalizedGreeting>(new Employee { Name = "Ada" }, opt => opt.Items["culture"] = "nb-NO");

        return Demo("ForCtorParam & ConstructUsing with Context", "Configure constructor parameters by name, or build the destination from the source and the resolution context.",
            new { Shipment = source, Employee = new Employee { Name = "Ada" }, Items = new { culture = "nb-NO" } },
            new { label, greeting }, "Constructors",
            "CreateMap<Shipment, ShippingLabel>()\n    .ForCtorParam(\"barcode\", o => o.MapFrom(s => \"LBL-\" + s.Reference))\n    .ForCtorParam(\"copies\", o => o.MapFrom(_ => 2));\n\nCreateMap<Employee, LocalizedGreeting>()\n    .ConstructUsing((s, ctx) => new LocalizedGreeting((string)ctx.Items[\"culture\"]));",
            "docs/features/constructor-mapping-and-records.md",
            "Carrier is matched to its constructor parameter by name. Barcode and copies come from ForCtorParam.",
            "Name on LocalizedGreeting is still mapped from the source after ConstructUsing.");
    }

    // 13.8
    public IActionResult MappingActions()
    {
        var source = new Document { Title = "Quarterly report" };
        var result = _engine.Map<Document, DocumentView>(source);

        return Demo("Mapping Actions", "A reusable AfterMap step in its own class, created from the container with its dependencies.",
            source, result, "MappingActions",
            "public class StampExportTime : IMappingAction<Document, DocumentView> { ... }\n\nCreateMap<Document, DocumentView>()\n    .ForMember(d => d.ExportedAtUtc, o => o.Ignore())\n    .AfterMap<StampExportTime>();",
            "docs/features/lifecycle-callbacks.md",
            "ExportedAtUtc comes from the ISampleClock service injected into StampExportTime.",
            "Assembly scanning registers mapping actions, so no manual AddTransient call is needed.");
    }

    // 13.9 and 13.10
    public IActionResult Converters()
    {
        var source = new Receipt { Total = 12345, TaxCents = 2469, Memo = "  paid in full  " };
        var result = _engine.Map<Receipt, ReceiptView>(source);

        return Demo("Value Converters & Member Transforms", "IValueConverter converts one member and can be reused across maps. AddTransform changes the final value of one member.",
            source, result, "Converters",
            "CreateMap<Receipt, ReceiptView>()\n    .ForMember(d => d.Total, o => o.ConvertUsing(new CentsToMoney()))\n    .ForMember(d => d.Tax, o => o.ConvertUsing(new CentsToMoney(), s => s.TaxCents))\n    .ForMember(d => d.Memo, o => { o.AddTransform(m => m.Trim()); o.AddTransform(m => m.ToUpperInvariant()); });",
            "docs/features/value-converters.md",
            "Total reads the source member with the same name. Tax reads TaxCents.",
            "Memo is trimmed and then upper-cased.");
    }

    // 13.11
    public IActionResult Inheritance()
    {
        var source = new List<MediaItem>
        {
            new Book { Title = "SICP", Pages = 657 },
            new AudioBook { Title = "Dune", Pages = 412, Minutes = 1268 },
            new Film { Title = "Hidden Figures", Runtime = 127 }
        };
        var result = _engine.Map<List<MediaItem>, List<MediaView>>(source);
        var gadget = _engine.Map<Gadget, GadgetView>(new Gadget { Name = "Pocket Lamp" });

        return Demo("IncludeAllDerived & As", "IncludeAllDerived dispatches to the closest derived map and shares the base configuration. As maps to a more specific destination type.",
            new { Media = source.Select(m => new { Type = m.GetType().Name, m.Title }), Gadget = "Pocket Lamp" },
            new { Media = result.Select(r => new { Type = r.GetType().Name, r.Title, r.Kind }), Gadget = new { Type = gadget.GetType().Name, gadget.Name } },
            "Inheritance",
            "CreateMap<MediaItem, MediaView>()\n    .IncludeAllDerived()\n    .ForMember(d => d.Kind, o => o.MapFrom(s => s.GetType().Name));\nCreateMap<Book, BookView>();\nCreateMap<AudioBook, AudioBookView>();\nCreateMap<Film, FilmView>();\n\nCreateMap<Gadget, GadgetView>().As<GadgetDetailView>();\nCreateMap<Gadget, GadgetDetailView>();",
            "docs/features/polymorphic-subclass-mapping.md",
            "AudioBook maps to AudioBookView, not BookView, because it is the closest match.",
            "Kind is set by the base map's rule, which every derived map inherits.");
    }

    // 13.12
    public IActionResult OpenGenerics()
    {
        var source = new PagedResult<Employee>
        {
            Items = { new Employee { Id = 1, Name = "Ada" }, new Employee { Id = 2, Name = "Grace" } },
            TotalCount = 42,
            PageNumber = 3
        };
        var result = _engine.Map<PagedResult<Employee>, PageView<EmployeeCard>>(source);
        var numbers = _engine.Map<PagedResult<int>, PageView<int>>(new PagedResult<int> { Items = { 7, 8 }, TotalCount = 2, PageNumber = 1 });

        return Demo("Open Generic Maps", "One map for every PagedResult<T> to PageView<T> pair, closed on first use.",
            new { Employees = source, Numbers = new { Items = new[] { 7, 8 }, TotalCount = 2, PageNumber = 1 } },
            new { Employees = result, Numbers = numbers },
            "OpenGenerics",
            "CreateMap(typeof(PagedResult<>), typeof(PageView<>))\n    .ForMember(\"Total\", o => o.MapFrom(\"TotalCount\"))\n    .ForMember(\"Page\", o => o.MapFrom(\"PageNumber\"));",
            "docs/features/open-generic-maps.md",
            "Employee to EmployeeCard uses its own map. Int items are copied.",
            "Members of an open generic map are configured by name.");
    }

    // 13.13
    public IActionResult NamingConventions()
    {
        var source = new LegacyCustomerRow();
        var result = _engine.Map<LegacyCustomerRow, CustomerRowView>(source);

        return Demo("Naming Conventions", "Destination prefixes, member name replacements, Get methods and camelCase all match without per-member configuration.",
            new { FullName = source.GetFullName(), source.Straße, source.emailAddress, source.Name }, result, "NamingConventions",
            "public class LegacyProfile : Profile\n{\n    public LegacyProfile()\n    {\n        RecognizeDestinationPrefixes(\"Dto\");\n        ReplaceMemberName(\"ß\", \"ss\");\n        CreateMap<LegacyCustomerRow, CustomerRowView>();\n    }\n}",
            "docs/features/naming-conventions.md",
            "ClearPrefixes() removes every source prefix, including the default Get.",
            "The Roslyn analyzer reads these module settings, so it agrees with the engine.", ParityConventions: true);
    }

    // 13.14
    public IActionResult Validation()
    {
        var result = ValidationDemo.Run();

        var model = DemoModel("Configuration Validation", "Validation uses the engine's own rules, so it reports what would actually go wrong.",
            "A deliberately broken configuration. See the module code below.", result.GenerateReport(), "Validation",
            "CreateMap<Wide, Narrow>(MemberList.Source);\nCreateMap<Holder, HolderView>();\nCreateMap<Narrow, Locked>();\n\nconfig.AssertConfigurationIsValid();",
            "docs/features/configuration-validation.md",
            "Each problem above names the map and the member. Nested automatic maps are checked too.",
            "Call AssertConfigurationIsValid() in a unit test to catch these before release.");
        model.SourceJson = "ValidationDemo.Run()";
        model.ResultJson = result.GenerateReport();
        return View("~/Views/Shared/_FeatureDemo.cshtml", model);
    }

    // 14.1 and 14.2
    public IActionResult Projections([FromServices] AutoMapSharp.Sample.Web.Data.SampleDbContext db, bool expand = false)
    {
        var query = expand
            ? db.Orders.OrderBy(o => o.Id).ProjectOnto<OrderCardDto>(_engine, null, d => d.Items)
            : db.Orders.OrderBy(o => o.Id).ProjectOnto<OrderCardDto>(_engine);
        var orders = query.ToList();

        var catalog = new List<CatalogEntry> { new Course { Id = 1, Title = "EF Core in Depth", Hours = 12 }, new Workshop { Id = 2, Title = "Mapping Clinic", City = "Oslo" } }.AsQueryable();
        var catalogQuery = catalog.ProjectOnto<CatalogEntryView>(_engine);

        var model = DemoModel("Projection Features", "Fallback becomes COALESCE, members can be expanded with expressions, and Include hierarchies become type checks.",
            new { ExpandItems = expand, Catalog = catalog.Select(c => new { Type = c.GetType().Name, c.Title }) },
            new { Orders = orders, Catalog = catalogQuery.ToList().Select(c => new { Type = c.GetType().Name, c.Title }) },
            "Projections",
            "CreateProjection<DbOrder, OrderCardDto>()\n    .ForMember(d => d.CustomerEmail, o => { o.MapFrom(s => s.Customer.Email); o.NullSubstitute(\"(no email)\"); })\n    .ForMember(d => d.Items, o => o.ExplicitExpansion());\n\nquery.ProjectTo<OrderCardDto>(config, null, d => d.Items);",
            "docs/features/orm-projection-overview.md",
            "Resolvers, converters and conditions run .NET code, so a projection that uses them now throws a clear error instead of silently ignoring them.",
            "The SQL below is generated by EF Core. Turn on expansion with ?expand=true.");
        model.ResultJson += "\n\n-- SQL\n" + query.ToQueryString() + "\n\n-- Catalog projection expression\n" + catalogQuery.Expression;
        model.FormAction = "/Parity/Projections";
        model.FormFields = new List<DemoField> { new() { Name = "expand", Label = "Expand Items (true or false)", Value = expand ? "true" : "false" } };
        return View("~/Views/Shared/_FeatureDemo.cshtml", model);
    }

    // 14.3
    public IActionResult Dictionaries()
    {
        var form = new Dictionary<string, object?>
        {
            ["name"] = "Margaret Hamilton",
            ["age"] = "33",
            ["subscribe"] = "true",
            ["address"] = new Dictionary<string, object?> { ["city"] = "Cambridge" }
        };
        var submission = _engine.Map<Dictionary<string, object?>, FormSubmission>(form);
        IDictionary<string, object?> expando = _engine.Map<FormSubmission, System.Dynamic.ExpandoObject>(submission);

        var model = DemoModel("Dictionaries & ExpandoObject", "Map string-keyed dictionaries and ExpandoObject to objects and back. Keys match members ignoring case, and values are converted.",
            form, new { Submission = submission, BackToExpando = expando.Keys }, "Projections",
            "var submission = mapper.Map<FormSubmission>(dictionary);\nvar expando = mapper.Map<ExpandoObject>(submission);",
            "docs/features/dictionary-mapping.md",
            "\"33\" and \"true\" are converted to int and bool. The nested dictionary becomes an Address.",
            "No map needs to be defined. Defined maps from dictionaries are skipped by validation, because keys are only known at run time.");
        model.ModuleSnippet = "// No configuration needed.\nvar submission = engine.Map<Dictionary<string, object?>, FormSubmission>(form);\nIDictionary<string, object?> expando = engine.Map<FormSubmission, ExpandoObject>(submission);";
        return View("~/Views/Shared/_FeatureDemo.cshtml", model);
    }

    // 14.4
    public IActionResult ExtensionMethods()
    {
        var source = new Member { First = "Barbara", Last = "Liskov", Visits = { new DateTime(2026, 10, 1), new DateTime(2026, 11, 2) } };
        var result = _engine.Map<Member, MemberBadge>(source);

        return Demo("Source Extension Methods", "Extension methods on the source type act as source members.",
            source, result, "ExtensionMethods",
            "cfg.IncludeSourceExtensionMethods(typeof(MemberExtensions));\ncfg.CreateMap<Member, MemberBadge>();",
            "docs/features/source-extension-methods.md",
            "Methods are matched by name, or by name with a recognized prefix such as Get.",
            "The analyzer reads IncludeSourceExtensionMethods too, so these members are not reported as unmapped.");
    }

    // 14.5
    public IActionResult Attributes()
    {
        var source = new SupportTicket { Number = 4711, Subject = "Printer on fire", AssignedAgent = "Grace", InternalNotes = "VIP" };
        var result = _engine.Map<SupportTicket, SupportTicketView>(source);
        var back = _engine.Map<SupportTicketView, SupportTicket>(result);

        var model = DemoModel("Attribute Configuration", "Define a map with [MapFrom] on the destination class, and configure members with [SourceMember] and [SkipMap].",
            source, new { View = result, ReverseMapped = back }, "Projections",
            "[AutoMap(typeof(SupportTicket), ReverseMap = true)]\npublic class SupportTicketView\n{\n    [SourceMember(nameof(SupportTicket.AssignedAgent))]\n    public string Owner { get; set; }\n\n    [Ignore]\n    public string InternalNotes { get; set; }\n}",
            "docs/features/attribute-mapping.md",
            "DI assembly scanning registers [MapFrom] maps, so there is no module for this map.",
            "[SkipMap] and [SourceMember] also apply to maps defined in modules, unless the module configures the member itself.");
        model.ModuleSnippet = "[MapFrom(typeof(SupportTicket), ReverseMap = true)]\npublic class SupportTicketView\n{\n    public int Number { get; set; }\n    public string Subject { get; set; } = \"\";\n\n    [SourceMember(nameof(SupportTicket.AssignedAgent))]\n    public string Owner { get; set; } = \"\";\n\n    [SkipMap]\n    public string? InternalNotes { get; set; }\n}";
        return View("~/Views/Shared/_FeatureDemo.cshtml", model);
    }

    // 14.6
    public IActionResult ExecutionPlan()
    {
        var plan = _engine.BuildExecutionPlan(typeof(Member), typeof(MemberBadge));
        var debugView = typeof(System.Linq.Expressions.Expression).GetProperty("DebugView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(plan) as string ?? plan.ToString();

        var model = DemoModel("Execution Plans", "See the expression tree the engine compiles for a type pair, and compile every map at start-up.",
            "engine.BuildExecutionPlan(typeof(Member), typeof(MemberBadge))", debugView, "ExtensionMethods",
            "var plan = config.BuildExecutionPlan(typeof(Member), typeof(MemberBadge));\nconfig.CompileMappings();",
            "docs/features/execution-plans.md",
            "The plan shows construction, each member assignment and the error tracking stage.",
            "Polymorphic dispatch, type converters, PreserveReferences and MaxDepth run around this plan.");
        return View("~/Views/Shared/_FeatureDemo.cshtml", model);
    }

    // 14.7
    public IActionResult CollectionMerge()
    {
        var cart = new Cart { Lines = { new CartLine { Sku = "PEN", Quantity = 1 }, new CartLine { Sku = "INK", Quantity = 2 } } };
        var penTracking = cart.Lines[0].TrackingId;
        var lines = cart.Lines;
        var before = cart.Lines.Select(l => new { l.Sku, l.Quantity, l.TrackingId }).ToList();

        var update = new CartDto { Lines = { new CartLineDto { Sku = "PEN", Quantity = 5 }, new CartLineDto { Sku = "PAD", Quantity = 1 } } };
        _engine.Map(update, cart);

        return Demo("Collection Merging", "With EqualityComparison, mapping onto an existing collection updates matching items in place, adds new ones and removes stale ones.",
            new { Before = before, Update = update },
            new { After = cart.Lines.Select(l => new { l.Sku, l.Quantity, l.TrackingId }), SameListInstance = ReferenceEquals(lines, cart.Lines), PenKeptItsTrackingId = cart.Lines[0].TrackingId == penTracking },
            "CollectionMerge",
            "// AutoMapper.Collection\ncfg.AddCollectionMappers();\ncfg.CreateMap<CartLineDto, CartLine>()\n    .EqualityComparison((dto, line) => dto.Sku == line.Sku);",
            "docs/features/collection-merging.md",
            "PEN is updated in place and keeps its TrackingId. INK is removed. PAD is added.",
            "This keeps EF Core tracked entities instead of replacing them, so EF Core issues UPDATE, INSERT and DELETE correctly.");
    }

    public IActionResult Matrix()
    {
        return View(ReadMatrix());
    }

    internal static List<ParityRow> ReadMatrix()
    {
        using var stream = typeof(ParityController).Assembly.GetManifestResourceStream("Docs.automapper-parity-matrix.md")!;
        using var reader = new StreamReader(stream);
        var rows = new List<ParityRow>();
        foreach (var line in reader.ReadToEnd().Split('\n').Select(l => l.Trim()))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal) || line.StartsWith("| Area", StringComparison.Ordinal) || line.StartsWith("|---", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|').Select(c => Clean(c.Trim())).ToArray();
            if (cells.Length >= 5)
            {
                rows.Add(new ParityRow { Area = cells[0], AutoMapper = cells[1], AutoMapSharp = cells[2], Status = cells[3], Notes = cells[4] });
            }
        }

        return rows;
    }

    private static string Clean(string markdown) => Regex.Replace(markdown, @"\[([^\]]+)\]\([^)]+\)", "$1").Replace("`", string.Empty);

    private IActionResult Demo(string title, string summary, object source, object result, string snippet, string autoMapper, string doc, string note1, string note2, bool ParityConventions = false)
        => View("~/Views/Shared/_FeatureDemo.cshtml", DemoModel(title, summary, source, result, snippet, autoMapper, doc, note1, note2));

    private static FeatureDemoViewModel DemoModel(string title, string summary, object source, object result, string snippet, string autoMapper, string doc, string note1, string note2)
    {
        return new FeatureDemoViewModel
        {
            Title = title,
            Summary = summary,
            FixedIn = "Added for AutoMapper parity.",
            SourceJson = source as string ?? FeatureDemoViewModel.ToJson(source),
            ResultJson = result as string ?? FeatureDemoViewModel.ToJson(result),
            ModuleSnippet = SnippetReader.Read(ModulesFile, snippet),
            AutoMapperSnippet = autoMapper,
            DocPath = doc,
            Notes = new List<string> { note1, note2 }
        };
    }
}
