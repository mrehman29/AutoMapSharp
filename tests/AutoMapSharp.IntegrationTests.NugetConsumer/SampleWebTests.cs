using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Controllers;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using AutoMapSharp.Sample.Web.Modules;
using AutoMapSharp.Sample.Web.Infrastructure;
using AutoMapSharp.Extensions.DependencyInjection;
using AutoMapSharp.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.IntegrationTests.NugetConsumer;

public class SampleWebTests
{
    private readonly IMapEngine _engine;

    public SampleWebTests()
    {
        MockDatabase.ResetData();

        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateService, MockTaxRateService>();
        services.AddTransient<WebTaxResolver>();
        services.AddSingleton<ISampleClock, FixedSampleClock>();
        var provider = services.BuildServiceProvider();

        _engine = new MapEngine(new MappingModule[]
        {
            new OrderWebModule(),
            new ExternalUserWebModule(),
            new LegacyCatalogWebModule(),
            new CustomerRecordWebModule(),
            new FluentWebModule(),
            new Phase4WebModule(),
            new Phase5WebModule(),
            new Phase6WebModule(),
            new ReliabilityWebModule(),
            new ParityWebModule(),
            new ParityFiltersWebModule(),
            new ParityConventionsWebModule(),
            new ParityTier2WebModule(),
            new ParityCollectionsWebModule(),
            new AttributeMappingModule(typeof(OrderWebModule).Assembly)
        }, provider);
    }

    private static SampleDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite("Data Source=test_consumer_automapsharp.db")
            .Options;

        var db = new SampleDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        db.SeedInitialData();
        return db;
    }

    [Fact]
    public void OrdersController_Index_RendersComparisonWithFlattenedAndConvertedData()
    {
        var controller = new OrdersController(_engine);
        var result = controller.Index(1001);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<Order, OrderSummaryViewModel>>(viewResult.Model);

        Assert.NotNull(model.Source);
        Assert.NotNull(model.Destination);
        Assert.Equal(1001, model.Destination.Id);
        Assert.Equal("Acme Global Industries", model.Destination.CustomerName);
        Assert.Equal("Redwood Shores", model.Destination.CustomerAddressCity);
        Assert.Equal("Signature required upon delivery.", model.Destination.SpecialInstructions);
        Assert.Equal("Completed", model.Destination.Status);
        Assert.Equal("a81f38d4-5178-4e5a-939e-21ef65d83a12", model.Destination.TrackingCode);
        Assert.Equal(1249.99m, model.Destination.TotalAmount);
    }

    [Fact]
    public void OrdersController_ApplyUpdate_MutatesExistingEntityInPlace()
    {
        var controller = new OrdersController(_engine);
        var request = new UpdateOrderViewModel
        {
            OrderId = 1001,
            TotalAmount = 3500.00m,
            Status = OrderStatus.Cancelled
        };

        var result = controller.ApplyUpdate(request);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<UpdateOrderViewModel, Order>>(viewResult.Model);

        Assert.True(model.IsInPlaceUpdate);
        Assert.True(model.ReferenceEqual == true, "Must preserve reference equality on existing entity update.");
        Assert.Equal(3500.00m, model.Destination.TotalAmount);
        Assert.Equal(OrderStatus.Cancelled, model.Destination.Status);
    }

    [Fact]
    public void OrdersController_NullSafe_DemonstratesSafeShortCircuit()
    {
        var controller = new OrdersController(_engine);
        var result = controller.NullSafe();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<Order, OrderSummaryViewModel>>(viewResult.Model);

        Assert.Equal(1003, model.Destination.Id);
        Assert.Null(model.Destination.CustomerName);
        Assert.Null(model.Destination.CustomerAddressCity);
    }

    [Fact]
    public void ConventionsController_SnakeCase_MapsToPascalCaseViewModel()
    {
        var controller = new ConventionsController(_engine);
        var result = controller.SnakeCase();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<ExternalWebhookUser, InternalUserViewModel>>(viewResult.Model);

        Assert.Equal("Eleanor", model.Destination.FirstName);
        Assert.Equal("Vance", model.Destination.LastName);
        Assert.Equal(34, model.Destination.UserAge);
        Assert.Equal("eleanor.vance@hillhouse.org", model.Destination.EmailAddress);
    }

    [Fact]
    public void ConventionsController_PrefixSuffix_StripsHungarianPrefixAndSuffix()
    {
        var controller = new ConventionsController(_engine);
        var result = controller.PrefixSuffix();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<LegacyProduct, ProductViewModel>>(viewResult.Model);

        Assert.Equal("Enterprise High-Performance Rack Server", model.Destination.Title);
        Assert.Equal("DataCenter Infrastructure", model.Destination.Category);
        Assert.Equal(4999.00m, model.Destination.Price);
    }

    [Fact]
    public void RecordsController_Index_MapsToPositionalRecord()
    {
        var controller = new RecordsController(_engine);
        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<WebCustomerSource, WebCustomerRecord>>(viewResult.Model);

        Assert.Equal(778, model.Destination.Id);
        Assert.Equal("Marcus Aurelius", model.Destination.FullName);
        Assert.Equal("Platinum Executive", model.Destination.Tier);
    }

    [Fact]
    public void DynamicController_Index_ResolvesUntypedAndDynamicMappings()
    {
        var controller = new DynamicController(_engine);
        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComparisonViewModel<object, WebCustomerRecord>>(viewResult.Model);

        Assert.NotNull(model.Destination);
        Assert.Equal(778, model.Destination.Id);
        Assert.Equal("Marcus Aurelius", model.Destination.FullName);
    }

    [Fact]
    public void FluentController_Overview_RendersSuccessfully()
    {
        var controller = new FluentController(_engine);
        var result = controller.Overview();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void FluentController_SkipAndConstants_SkipsSensitiveAndSetsConstants()
    {
        var controller = new FluentController(_engine);
        var result = controller.SkipAndConstants();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserProfileViewModel>(viewResult.Model);

        Assert.Null(model.PasswordHash);
        Assert.Null(model.InternalSecurityNotes);
        Assert.Equal("CORP-GLOBAL", model.TenantBadge);
    }

    [Fact]
    public void FluentController_MapSource_CalculatesComputedFields()
    {
        var controller = new FluentController(_engine);
        var result = controller.MapSource();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserProfileViewModel>(viewResult.Model);

        Assert.Equal("Ada Lovelace", model.DisplayName);
        Assert.Equal("12 St. James's Square, London SW1Y 4LB", model.FullAddress);
    }

    [Fact]
    public void FluentController_Conditionals_AppliesIfConditionAndFallback()
    {
        var controller = new FluentController(_engine);
        var formModel = new ConditionFormModel
        {
            Age = 30,
            AnnualSpend = 2500m,
            Bio = null
        };

        var result = controller.Conditionals(formModel);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ConditionResultViewModel>(viewResult.Model);

        Assert.Equal("Gold Premium Member", model.MemberStatus);
        Assert.Equal("Default bio: Passionate shopper.", model.Bio);
    }

    [Fact]
    public void FluentController_ReverseMap_SymmetricMapping()
    {
        var controller = new FluentController(_engine);

        // Forward
        var forwardResult = controller.ReverseMap("forward");
        var forwardView = Assert.IsType<ViewResult>(forwardResult);
        var mappedDto = Assert.IsType<ReverseEmployeeDto>(forwardView.ViewData["MappedDto"]);
        Assert.Equal("Katherine Johnson", mappedDto.DisplayName);
        Assert.Null(mappedDto.ConfidentialCode);

        // Reverse
        var reverseResult = controller.ReverseMap("reverse");
        var reverseView = Assert.IsType<ViewResult>(reverseResult);
        var mappedEntity = Assert.IsType<ReverseEmployeeEntity>(reverseView.ViewData["MappedEntity"]);
        Assert.Equal("Margaret Hamilton", mappedEntity.FullName);
        Assert.Equal("REVERSE-RESTORED", mappedEntity.ConfidentialCode);
    }

    [Fact]
    public void FluentController_ValueTransformers_SanitizesWhitespace()
    {
        var controller = new FluentController(_engine);
        var result = controller.ValueTransformers();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CleanTextViewModel>(viewResult.Model);

        Assert.Equal("Clean Architecture in .NET 9", model.Title);
        Assert.Equal("A comprehensive guide with zero excess whitespace.", model.Description);
        Assert.Equal("csharp, dotnet, automapsharp, performance", model.Tags);
    }

    [Fact]
    public void CollectionsController_Standard_MapsLineItems()
    {
        var controller = new CollectionsController(_engine);
        var result = controller.Standard();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebMultiItemOrderViewModel>(viewResult.Model);

        Assert.Equal(8001, model.OrderId);
        Assert.Equal("Apollo Space Exploration", model.CustomerName);
        Assert.NotNull(model.Items);
        Assert.Equal(3, model.Items.Count);
        Assert.Equal("Thermal Heat Shield Tile", model.Items[0].ProductName);
        Assert.Equal(450.00m, model.Items[0].UnitPrice);
        Assert.Equal(4, model.Items[0].Quantity);
        Assert.Equal(1800.00m, model.Items[0].LineTotal);
    }

    [Fact]
    public void CollectionsController_DictionariesAndSets_MapsKeyValuesAndUniqueTags()
    {
        var controller = new CollectionsController(_engine);
        var result = controller.DictionariesAndSets();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebMultiItemOrderViewModel>(viewResult.Model);

        Assert.NotNull(model.Tags);
        Assert.Equal(3, model.Tags.Count); // Deduplicated
        Assert.Contains("Aerospace", model.Tags);
        Assert.Contains("Cryogenic", model.Tags);
        Assert.Contains("Mission-Critical", model.Tags);

        Assert.NotNull(model.Specs);
        Assert.Equal("0.001mm", model.Specs["Tolerance"]);
        Assert.Equal("Ti-6Al-4V", model.Specs["Material"]);
        Assert.Equal("MIL-STD-810G", model.Specs["Compliance"]);
    }

    [Fact]
    public void CollectionsController_NullCollections_DemonstratesNullVsEmpty()
    {
        var controller = new CollectionsController(_engine);
        var result = controller.NullCollections();

        var viewResult = Assert.IsType<ViewResult>(result);
        var defaultResult = Assert.IsType<WebMultiItemOrderViewModel>(viewResult.ViewData["DefaultResult"]);
        Assert.NotNull(defaultResult.Items);
        Assert.Empty(defaultResult.Items);

        var nullResult = Assert.IsType<WebMultiItemOrderViewModel>(viewResult.ViewData["NullResult"]);
        Assert.Null(nullResult.Items);
    }

    [Fact]
    public void CollectionsController_InPlaceUpdate_MutatesExistingCollection()
    {
        var controller = new CollectionsController(_engine);
        var result = controller.InPlaceUpdate();

        var viewResult = Assert.IsType<ViewResult>(result);
        var destination = Assert.IsType<WebMultiItemOrderViewModel>(viewResult.Model);

        Assert.NotNull(destination.Items);
        Assert.Equal(3, destination.Items.Count);
        Assert.Equal("Thermal Heat Shield Tile", destination.Items[0].ProductName);
        Assert.True(viewResult.ViewData["ReferencePreserved"] as bool? == true);
    }

    [Fact]
    public void ComplexTypesController_DeepChaining_Maps4LevelHierarchy()
    {
        var controller = new ComplexTypesController(_engine);
        var result = controller.DeepChaining();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebOrgEnterpriseViewModel>(viewResult.Model);

        Assert.Equal("Alphabet Inc.", model.Name);
        Assert.NotNull(model.Division);
        Assert.Equal("Google DeepMind", model.Division.Name);
        Assert.NotNull(model.Division.Department);
        Assert.Equal("Advanced Agentic Coding Group", model.Division.Department.Name);
        Assert.NotNull(model.Division.Department.Manager);
        Assert.Equal("Demis Hassabis", model.Division.Department.Manager.Name);
        Assert.Equal("Chief Executive Officer & Co-Founder", model.Division.Department.Manager.Title);
        Assert.Equal("demis@deepmind.com", model.Division.Department.Manager.Email);
    }

    [Fact]
    public void ComplexTypesController_ConstructUsing_InstantiatesImmutableObject()
    {
        var controller = new ComplexTypesController(_engine);
        var result = controller.ConstructUsing();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebImmutableProduct>(viewResult.Model);

        Assert.Equal("QUANTUM-SERVER-9000", model.SkuCode);
        Assert.Equal(4725.00m, model.RetailPrice);
        Assert.Equal("ENTERPRISE-TIER", model.TierTag);
    }

    [Fact]
    public void ComplexTypesController_CircularReferences_PreservesReferenceIdentity()
    {
        var controller = new ComplexTypesController(_engine);
        var result = controller.CircularReferences();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebCircularEmployeeViewModel>(viewResult.Model);

        Assert.Equal(1, model.Id);
        Assert.Equal("Grace Hopper", model.Name);
        Assert.Single(model.Subordinates);

        var lead = model.Subordinates[0];
        Assert.Equal(2, lead.Id);
        Assert.Equal("Alan Turing", lead.Name);

        // Circular reference identity check:
        Assert.NotNull(lead.Manager);
        Assert.Same(model, lead.Manager);
        Assert.True(viewResult.ViewData["IdentityPreserved"] as bool? == true);
    }

    [Fact]
    public void ResolversController_Index_ResolvesTaxViaCustomResolver()
    {
        var controller = new ResolversController(_engine);
        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebOrderTaxViewModel>(viewResult.Model);

        Assert.Equal(9101, model.OrderId);
        Assert.Equal("Silicon Valley Innovations Inc.", model.CustomerName);
        Assert.Equal(12500.00m, model.Subtotal);
        Assert.Equal("CA", model.ShippingState);
        Assert.Equal(8.25m, model.TaxRatePercent);
        Assert.Equal(1031.25m, model.CalculatedTax);
        Assert.Equal(13531.25m, model.TotalAmount);
    }

    [Fact]
    public void ResolversController_TypeConverters_ParsesRawJsonToViewModel()
    {
        var controller = new ResolversController(_engine);
        var result = controller.TypeConverters();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebParsedJsonViewModel>(viewResult.Model);

        Assert.Equal("DOC-CFG-9981", model.DocumentId);
        Assert.Equal("Production-Kube-US-West", model.ClusterName);
        Assert.Equal(32, model.NodeCount);
        Assert.Equal("us-west-2", model.Region);
        Assert.True(model.HighAvailability);
    }

    [Fact]
    public void PolymorphicController_Billing_MapsHeterogeneousPaymentList()
    {
        var controller = new PolymorphicController(_engine);
        var result = controller.Billing();

        var viewResult = Assert.IsType<ViewResult>(result);
        var payments = Assert.IsType<List<WebPaymentViewModel>>(viewResult.Model);

        Assert.Equal(3, payments.Count);

        var cc = Assert.IsType<WebCreditCardPaymentViewModel>(payments[0]);
        Assert.Equal("Credit Card", cc.PaymentType);
        Assert.Equal("Grace Brewster Hopper", cc.CardHolder);
        Assert.Equal("************4242", cc.MaskedCardNumber);
        Assert.Equal("Visa Infinite", cc.CardNetwork);

        var crypto = Assert.IsType<WebCryptoPaymentViewModel>(payments[1]);
        Assert.Equal("Cryptocurrency", crypto.PaymentType);
        Assert.Equal("0x71C...8976B3921", crypto.WalletAddress);
        Assert.Equal("Ethereum Mainnet", crypto.BlockchainNetwork);
        Assert.Equal(0.0042m, crypto.GasFee);

        var wire = Assert.IsType<WebBankTransferPaymentViewModel>(payments[2]);
        Assert.Equal("Bank Wire", wire.PaymentType);
        Assert.Equal("JPMorgan Chase Bank, N.A.", wire.BankName);
        Assert.Equal("*****0021", wire.RoutingNumberMasked);
    }

    [Fact]
    public void LifecycleController_Hooks_ExecutesBeforeAndAfterMapHooks()
    {
        var controller = new LifecycleController(_engine);
        var result = controller.Hooks();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WebAuditedViewModel>(viewResult.Model);

        Assert.Equal(4402, model.EntityId);
        Assert.Equal("Deep Space Autonomous Probe Telemetry", model.EntityName);
        Assert.Equal("SEC-***-REDACTED", model.MaskedSecurityCode);
        Assert.Contains("[AUDIT-START: Initiated mapping for Deep Space Autonomous Probe Telemetry]", model.AuditTrail);
        Assert.Contains("[AUDIT-FINISH: Completed]", model.AuditTrail);
        Assert.Equal(new DateTime(2026, 11, 4, 18, 0, 0, DateTimeKind.Utc), model.MappedTimestampUtc);
    }

    [Fact]
    public void DiagnosticsController_HealthCheck_ReportsValidConfiguration()
    {
        var controller = new DiagnosticsController(_engine);
        var result = controller.HealthCheck();

        var viewResult = Assert.IsType<ViewResult>(result);
        var validation = Assert.IsType<ConfigurationValidationResult>(viewResult.Model);

        Assert.True(validation.IsValid, validation.GenerateReport());
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void ProjectionsController_Index_ProjectsOrdersFromSqliteDatabase()
    {
        using var db = CreateTestDbContext();
        var controller = new ProjectionsController(db, _engine);
        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var orders = Assert.IsType<List<OrderProjectionDto>>(viewResult.Model);

        Assert.Equal(3, orders.Count);
        var first = orders[0];
        Assert.Equal(1, first.Id);
        Assert.Equal("ORD-2026-8801", first.OrderNumber);
        Assert.Equal(2450.00m, first.TotalAmount);
        Assert.Equal("Katherine Johnson", first.CustomerName);
        Assert.Equal("San Francisco", first.CustomerAddressCity);
        Assert.Equal("CORP-ENTERPRISE-HQ", first.TenantBadge);

        var sql = Assert.IsType<string>(viewResult.ViewData["GeneratedSql"]);
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void ProjectionsController_SqlInspector_GeneratesBothQueries()
    {
        using var db = CreateTestDbContext();
        var controller = new ProjectionsController(db, _engine);
        var result = controller.SqlInspector();

        var viewResult = Assert.IsType<ViewResult>(result);
        var naiveSql = Assert.IsType<string>(viewResult.ViewData["NaiveSql"]);
        var projectedSql = Assert.IsType<string>(viewResult.ViewData["ProjectedSql"]);

        Assert.Contains("SELECT", naiveSql);
        Assert.Contains("SELECT", projectedSql);
    }

    [Fact]
    public void ReliabilityController_NullableValues_PreservesEveryValue()
    {
        var controller = new ReliabilityController(_engine);
        var result = controller.NullableValues();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<FeatureDemoViewModel>(viewResult.Model);
        var source = ReliabilityController.CreateSampleShipment();

        Assert.Equal(model.SourceJson, model.ResultJson);
        Assert.Contains("\"CustomerRating\": 4", model.ResultJson);
        Assert.Contains("\"TransitTime\": \"1.14:30:00\"", model.ResultJson);
        Assert.Contains("DefineMap<ShipmentRecord, ShipmentViewModel>()", model.ModuleSnippet);
        Assert.Equal(true, viewResult.ViewData["SameMetadataInstance"]);

        var direct = _engine.Map<ShipmentRecord, ShipmentViewModel>(source);
        Assert.Equal(source.DeclaredValue, direct.DeclaredValue);
        Assert.Equal(source.TrackingId, direct.TrackingId);
        Assert.Equal(source.ShipDate, direct.ShipDate);
        Assert.Equal(12.75m, direct.Weight.Kilograms);
    }

    private static FeatureDemoViewModel DemoModel(IActionResult result) =>
        Assert.IsType<FeatureDemoViewModel>(Assert.IsType<ViewResult>(result).Model);

    [Fact]
    public void ReliabilityController_RecordsNested_MapsNestedRecordsThroughConstructor()
    {
        var model = DemoModel(new ReliabilityController(_engine).RecordsNested());

        Assert.Contains("\"CustomerName\": \"Grace Hopper\"", model.ResultJson);
        Assert.Contains("\"City\": \"Arlington\"", model.ResultJson);
        Assert.Contains("\"BillingAddress\": null", model.ResultJson);
        Assert.Contains("\"Sku\": \"UNIVAC-I\"", model.ResultJson);

        var record = _engine.Map<PurchaseOrderEntity, PurchaseOrderRecord>(new PurchaseOrderEntity
        {
            Customer = new CustomerEntity { Name = "A" },
            ShippingAddress = new AddressEntity { City = "X" },
            Lines = { new() { Sku = "S", Quantity = 3 } }
        });
        Assert.Equal(3, record.Lines[0].Quantity);
    }

    [Fact]
    public void ReliabilityController_ReverseUnflatten_MapsFormBackIntoNestedEntity()
    {
        var model = DemoModel(new ReliabilityController(_engine).ReverseUnflatten("Ann", "ann@example.com", "123", "Oslo"));

        Assert.Contains("\"Email\": \"ann@example.com\"", model.ResultJson);
        Assert.Contains("\"City\": \"Oslo\"", model.ResultJson);
        Assert.Equal(4, model.FormFields.Count);

        var entity = _engine.Map<CustomerAccountForm, CustomerAccount>(new CustomerAccountForm { Name = "Bo", ContactPhone = "9" });
        Assert.Equal("9", entity.Contact!.Phone);
    }

    [Fact]
    public void ReliabilityController_EnumByName_MatchesNamesAndFallsBackToValue()
    {
        var model = DemoModel(new ReliabilityController(_engine).EnumByName());

        Assert.Contains("Low ticket: Low (3)", model.ResultJson);
        Assert.Contains("Critical ticket: Critical (0)", model.ResultJson);
        Assert.Contains("Escalated ticket: Unknown (4)", model.ResultJson);
    }

    [Fact]
    public void ReliabilityController_CollectionShapes_FillsEveryCollectionType()
    {
        var model = DemoModel(new ReliabilityController(_engine).CollectionShapes());
        var view = _engine.Map<TagSource, CollectionShapesView>(new TagSource { Tags = { "b", "a", "b" }, Scores = { ["z"] = 1, ["y"] = 2 } });

        Assert.Equal(new[] { "b", "a", "b" }, view.Observable);
        Assert.Equal(new[] { "b", "a", "b" }, view.ReadOnly);
        Assert.Equal(new[] { "b", "a", "b" }, view.Immutable);
        Assert.Equal(new[] { "a", "b" }, view.Sorted);
        Assert.Equal(new[] { "b", "a", "b" }, view.Queue);
        Assert.Equal(2, view.Set.Count);
        Assert.Equal(3, view.Linked.Count);
        Assert.Equal(new[] { "y", "z" }, view.SortedScores.Keys);
        Assert.Contains("\"accuracy\": 10", model.ResultJson);
    }

    [Fact]
    public void ReliabilityController_SharedReferences_SharesPerDestinationType()
    {
        var result = Assert.IsType<ViewResult>(new ReliabilityController(_engine).SharedReferences());

        Assert.Equal(true, result.ViewData["LeadIsShared"]);
        var model = Assert.IsType<FeatureDemoViewModel>(result.Model);
        Assert.Contains("\"Spotlight\": {", model.ResultJson);
    }

    /// <summary>
    /// Composes the sample app's real DI registration, so a broken composition root, such as two scanned
    /// modules defining the same map, fails here instead of only at run time.
    /// </summary>
    [Fact]
    public void SampleApp_DiRegistration_BuildsEngineFromScannedAssembly()
    {
        var services = new ServiceCollection();
        services.AddSampleMappingServices();
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = root.CreateScope();

        var engine = scope.ServiceProvider.GetRequiredService<IMapEngine>();
        var view = engine.Map<AuditEntry, AuditEntryView>(new AuditEntry { Action = "x" });

        Assert.Equal(scope.ServiceProvider.GetRequiredService<RequestTrace>().RequestId, view.RequestId);
    }

    [Fact]
    public void ReliabilityController_ScopedResolver_UsesEachRequestsScopedService()
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestTrace>();
        services.AddAutoMapSharp(typeof(ReliabilityWebModule));
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        List<AuditEntryView> MapInNewRequest(out Guid requestId)
        {
            using var scope = root.CreateScope();
            requestId = scope.ServiceProvider.GetRequiredService<RequestTrace>().RequestId;
            var controller = new ReliabilityController(scope.ServiceProvider.GetRequiredService<IMapEngine>());
            DemoModel(controller.ScopedResolver());
            return scope.ServiceProvider.GetRequiredService<IMapEngine>()
                .Map<List<AuditEntry>, List<AuditEntryView>>(new List<AuditEntry> { new() { Action = "a" }, new() { Action = "b" } });
        }

        var first = MapInNewRequest(out var firstId);
        var second = MapInNewRequest(out var secondId);

        Assert.All(first, v => Assert.Equal(firstId, v.RequestId));
        Assert.All(second, v => Assert.Equal(secondId, v.RequestId));
        Assert.NotEqual(firstId, secondId);
    }

    // Parity pages

    private ParityController Parity() => new(_engine);

    [Fact]
    public void Parity_InlineConfiguration_TrimsAndUsesInlineValue()
    {
        var model = DemoModel(Parity().InlineConfiguration());
        Assert.Contains("\"Name\": \"Grace Hopper\"", model.ResultJson);
        Assert.Contains("defined inline", model.ResultJson);
    }

    [Fact]
    public void Parity_FieldsAndFilters_MapsFieldsAndAppliesFilters()
    {
        var view = _engine.Map<SensorReading, SensorView>(new SensorReading());
        Assert.Equal("TEMP-07", view.DeviceId);
        Assert.Equal(21.5, view.Celsius);
        Assert.Equal("Server room", view.Location);
        Assert.Null(view.SecretKey);
        Assert.Null(view.InternalNotes);
        DemoModel(Parity().FieldsAndFilters());
    }

    [Fact]
    public void Parity_Conditions_KeepsValuesThatFailTheirCondition()
    {
        var model = DemoModel(Parity().Conditions());
        Assert.Contains("\"Name\": \"Kept\"", model.ResultJson);
        Assert.Contains("\"Age\": 50", model.ResultJson);
        Assert.Contains("\"Nickname\": null", model.ResultJson);
    }

    [Fact]
    public void Parity_DestinationValues_KeepsOrReplacesPerMember()
    {
        var model = DemoModel(Parity().DestinationValues());
        Assert.Contains("\"PreferencesKeptSameInstance\": true", model.ResultJson);
        Assert.Contains("\"DraftReplaced\": true", model.ResultJson);
        Assert.Contains("\"Theme\": \"dark\"", model.ResultJson);
    }

    [Fact]
    public void Parity_PerCallOptions_UsesItemAndRunsCallbacks()
    {
        var model = DemoModel(Parity().PerCallOptions("NOK"));
        Assert.Contains("49.99 NOK", model.ResultJson);
        Assert.Contains("AfterMap: Total is 49.99 NOK", model.ResultJson);
    }

    [Fact]
    public void Parity_Constructors_UsesForCtorParamAndContext()
    {
        var model = DemoModel(Parity().Constructors());
        Assert.Contains("\"Barcode\": \"LBL-88213\"", model.ResultJson);
        Assert.Contains("\"Copies\": 2", model.ResultJson);
        Assert.Contains("\"Culture\": \"nb-NO\"", model.ResultJson);
    }

    [Fact]
    public void Parity_MappingActions_InjectsClock()
    {
        var view = _engine.Map<Document, DocumentView>(new Document { Title = "t" });
        Assert.Equal(new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc), view.ExportedAtUtc);
        DemoModel(Parity().MappingActions());
    }

    [Fact]
    public void Parity_Converters_ConvertAndTransform()
    {
        var model = DemoModel(Parity().Converters());
        Assert.Contains("123.45 USD", model.ResultJson);
        Assert.Contains("24.69 USD", model.ResultJson);
        Assert.Contains("PAID IN FULL", model.ResultJson);
    }

    [Fact]
    public void Parity_Inheritance_PicksClosestDerivedMapAndAs()
    {
        var model = DemoModel(Parity().Inheritance());
        Assert.Contains("\"Type\": \"AudioBookView\"", model.ResultJson);
        Assert.Contains("\"Type\": \"FilmView\"", model.ResultJson);
        Assert.Contains("\"Kind\": \"AudioBook\"", model.ResultJson);
        Assert.Contains("\"Type\": \"GadgetDetailView\"", model.ResultJson);
    }

    [Fact]
    public void Parity_OpenGenerics_ClosesForEachTypeArgument()
    {
        var model = DemoModel(Parity().OpenGenerics());
        Assert.Contains("\"Total\": 42", model.ResultJson);
        Assert.Contains("\"Page\": 3", model.ResultJson);
        Assert.Contains("\"Name\": \"Grace\"", model.ResultJson);
    }

    [Fact]
    public void Parity_NamingConventions_MatchesEveryRow()
    {
        var view = _engine.Map<LegacyCustomerRow, CustomerRowView>(new LegacyCustomerRow());
        Assert.Equal("Ada Lovelace", view.FullName);
        Assert.Equal("Main Street 1", view.Strasse);
        Assert.Equal("ada@example.com", view.EmailAddress);
        Assert.Equal("ada", view.DtoName);
        DemoModel(Parity().NamingConventions());
    }

    [Fact]
    public void Parity_Validation_ReportsEachProblem()
    {
        var model = DemoModel(Parity().Validation());
        Assert.Contains("Extra", model.ResultJson);
        Assert.Contains("Missing", model.ResultJson);
        Assert.Contains("Code", model.ResultJson);
        Assert.Contains("secretToken", model.ResultJson);
    }

    [Fact]
    public void Parity_Projections_TranslatesFallbackExpansionAndInheritance()
    {
        using var db = CreateTestDbContext();
        var collapsed = DemoModel(Parity().Projections(db, expand: false));
        var expanded = DemoModel(Parity().Projections(db, expand: true));

        Assert.Contains("COALESCE", collapsed.ResultJson);
        Assert.Contains("\"Items\": null", collapsed.ResultJson);
        Assert.Contains("Cryogenic Fuel Injector", expanded.ResultJson);
        Assert.Contains("\"Type\": \"WorkshopView\"", collapsed.ResultJson);
    }

    [Fact]
    public void Parity_Dictionaries_ConvertsValuesAndNestedDictionaries()
    {
        var form = _engine.Map<Dictionary<string, object?>, FormSubmission>(new Dictionary<string, object?> { ["age"] = "33", ["address"] = new Dictionary<string, object?> { ["city"] = "X" } });
        Assert.Equal(33, form.Age);
        Assert.Equal("X", form.Address!.City);
        Assert.Contains("Cambridge", DemoModel(Parity().Dictionaries()).ResultJson);
    }

    [Fact]
    public void Parity_ExtensionMethods_ReadExtensionMembers()
    {
        var badge = _engine.Map<Member, MemberBadge>(new Member { First = "A", Last = "B", Visits = { new DateTime(2026, 1, 1) } });
        Assert.Equal("A B", badge.FullName);
        Assert.Equal(1, badge.VisitCount);
        DemoModel(Parity().ExtensionMethods());
    }

    [Fact]
    public void Parity_Attributes_UseMapFromSourceMemberAndSkipMap()
    {
        var view = _engine.Map<SupportTicket, SupportTicketView>(new SupportTicket { AssignedAgent = "Grace", InternalNotes = "VIP" });
        Assert.Equal("Grace", view.Owner);
        Assert.Null(view.InternalNotes);
        DemoModel(Parity().Attributes());
    }

    [Fact]
    public void Parity_ExecutionPlan_ShowsCompiledExpression()
    {
        Assert.Contains("FullName", DemoModel(Parity().ExecutionPlan()).ResultJson);
    }

    [Fact]
    public void Parity_CollectionMerge_KeepsMatchedItems()
    {
        var model = DemoModel(Parity().CollectionMerge());
        Assert.Contains("\"SameListInstance\": true", model.ResultJson);
        Assert.Contains("\"PenKeptItsTrackingId\": true", model.ResultJson);
        Assert.DoesNotContain("\"Sku\": \"INK\"", model.ResultJson.Substring(model.ResultJson.IndexOf("After", StringComparison.Ordinal)));
    }

    [Fact]
    public void Parity_Matrix_RendersEveryRowOfTheDocsFile()
    {
        var rows = Assert.IsType<List<ParityRow>>(Assert.IsType<ViewResult>(Parity().Matrix()).Model);
        Assert.True(rows.Count > 50);
        Assert.Contains(rows, r => r.AutoMapSharp.Contains("ForPath") && r.Status == "Supported");
        Assert.All(rows, r => Assert.Contains(r.Status, new[] { "Supported", "Partial", "Planned", "Not planned" }));
    }

    [Fact]
    public void ProjectionsController_SafeParameters_EachValueGetsItsOwnResultAndSqlParameter()
    {
        using var db = CreateTestDbContext();
        var controller = new ProjectionsController(db, _engine);

        var result = controller.SafeParameters(minQuantity: 2);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SafeParametersViewModel>(viewResult.Model);
        var selected = model.SelectedResults.Single(r => r.OrderNumber == "ORD-2026-8801");
        var comparison = model.ComparisonResults.Single(r => r.OrderNumber == "ORD-2026-8801");

        Assert.Equal(1, model.ComparisonMinQuantity);
        Assert.Equal(1, selected.BulkLineCount);
        Assert.Equal(2, comparison.BulkLineCount);

        var select = model.GeneratedSql.Substring(model.GeneratedSql.IndexOf("SELECT", StringComparison.Ordinal));
        Assert.Contains("\"Quantity\" >= @", select);
        Assert.Contains("int minQuantity = 0;", model.ModuleSnippet);
    }

    [Fact]
    public void ProjectionsController_Filtered_AppliesThresholdFilter()
    {
        using var db = CreateTestDbContext();
        var controller = new ProjectionsController(db, _engine);

        // Filter for >= $2000 (should return 2 orders: $2450.00 and $5600.00)
        var result = controller.Filtered(2000m);

        var viewResult = Assert.IsType<ViewResult>(result);
        var orders = Assert.IsType<List<OrderProjectionDto>>(viewResult.Model);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.True(o.TotalAmount >= 2000m));
    }

    [Fact]
    public void ProjectionsController_ExplicitExpansion_TogglesItemExpansion()
    {
        using var db = CreateTestDbContext();
        var controller = new ProjectionsController(db, _engine);

        // 1. Without expansion: Items should be null
        var unexpandedResult = controller.ExplicitExpansion(expandItems: false, expandAudit: false);
        var unexpandedView = Assert.IsType<ViewResult>(unexpandedResult);
        var unexpandedOrders = Assert.IsType<List<OrderProjectionDto>>(unexpandedView.Model);
        Assert.All(unexpandedOrders, o => Assert.Null(o.Items));

        // 2. With expansion: Items should be populated
        var expandedResult = controller.ExplicitExpansion(expandItems: true, expandAudit: false);
        var expandedView = Assert.IsType<ViewResult>(expandedResult);
        var expandedOrders = Assert.IsType<List<OrderProjectionDto>>(expandedView.Model);
        var firstOrderItems = expandedOrders[0].Items;
        Assert.NotNull(firstOrderItems);
        Assert.Equal(2, firstOrderItems.Count);
        Assert.Equal("Cryogenic Fuel Injector", firstOrderItems[0].ProductName);
        Assert.Equal(1700.00m, firstOrderItems[0].LineTotal);
    }

    [Fact]
    public void MigrationController_Analyzers_ReturnsAllRoslynRules()
    {
        var controller = new MigrationController();
        var result = controller.Analyzers();

        var viewResult = Assert.IsType<ViewResult>(result);
        var rules = Assert.IsType<List<AnalyzerRuleViewModel>>(viewResult.Model);

        Assert.Equal(3, rules.Count);
        Assert.Contains(rules, r => r.Id == "AMS001");
        Assert.Contains(rules, r => r.Id == "AMS002");
        Assert.Contains(rules, r => r.Id == "AMS003");
    }

    [Fact]
    public void MigrationController_CodeFixes_ReturnsActionableBeforeAndAfterFixes()
    {
        var controller = new MigrationController();
        var result = controller.CodeFixes();

        var viewResult = Assert.IsType<ViewResult>(result);
        var fixes = Assert.IsType<List<CodeFixViewModel>>(viewResult.Model);

        Assert.NotEmpty(fixes);
        Assert.All(fixes, f =>
        {
            Assert.False(string.IsNullOrEmpty(f.BeforeCode));
            Assert.False(string.IsNullOrEmpty(f.AfterCode));
            Assert.Contains(".ForProperty", f.AfterCode);
        });
    }

    [Fact]
    public void MigrationController_AutoMapperScanner_DetectsLegacyConstructs()
    {
        var controller = new MigrationController();
        var inputModel = new MigrationScannerViewModel
        {
            InputCode = @"using AutoMapper;

public class TestProfile : Profile
{
    public TestProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.CustomerName))
            .ForMember(d => d.Secret, opt => opt.Ignore());
    }
}"
        };

        var result = controller.AutoMapperScanner(inputModel);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MigrationScannerViewModel>(viewResult.Model);

        Assert.True(model.HasRun);
        Assert.True(model.DetectedDiagnostics.Count >= 5);
        Assert.Contains(model.DetectedDiagnostics, d => d.RuleId == "AMSMIG001");
        Assert.Contains(model.DetectedDiagnostics, d => d.RuleId == "AMSMIG002");
        Assert.Contains(model.DetectedDiagnostics, d => d.RuleId == "AMSMIG003");
        Assert.Contains(model.DetectedDiagnostics, d => d.RuleId == "AMSMIG004");
        Assert.Contains(model.DetectedDiagnostics, d => d.RuleId == "AMSMIG005");
        Assert.Contains(model.DetectedDiagnostics, d => d.RuleId == "AMSMIG006");
    }

    [Fact]
    public void MigrationController_Playground_DefaultSampleShowsEveryRewrite()
    {
        var model = Assert.IsType<MigrationPlaygroundViewModel>(Assert.IsType<ViewResult>(new MigrationController().Playground()).Model);

        Assert.DoesNotContain("AutoMapSharp.Projections", model.MigratedCode);
        Assert.Contains("opt.If(src => src.IsMember)", model.MigratedCode);
        Assert.Contains("opt.Fallback(\"(none)\")", model.MigratedCode);
        Assert.Contains("opt.MapWith<TaxResolver>()", model.MigratedCode);
        Assert.Contains("orders.ProjectOnto<OrderDto>(mapper)", model.MigratedCode);
        Assert.Contains("IPropertyResolver<OrderEntity, OrderDto, decimal>", model.MigratedCode);
    }

    [Fact]
    public void MigrationController_Playground_TransformsCodeInRealTime()
    {
        var controller = new MigrationController();
        var inputModel = new MigrationPlaygroundViewModel
        {
            OriginalCode = @"using AutoMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Title))
            .ForMember(d => d.Id, opt => opt.Ignore());
    }
}"
        };

        var result = controller.Playground(inputModel);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MigrationPlaygroundViewModel>(viewResult.Model);

        Assert.True(model.WasMigrated);
        Assert.True(model.TotalReplacements >= 5);
        Assert.Contains("using AutoMapSharp;", model.MigratedCode);
        Assert.Contains("class OrderProfile : MappingModule", model.MigratedCode);
        Assert.Contains("DefineMap<Order, OrderDto>()", model.MigratedCode);
        Assert.Contains(".ForProperty(d => d.Name, opt => opt.MapSource(s => s.Title))", model.MigratedCode);
        Assert.Contains(".ForProperty(d => d.Id, opt => opt.Skip())", model.MigratedCode);
    }

    [Fact]
    public void BenchmarksController_Results_ShowsMeasuredNumbersNotWithdrawnClaims()
    {
        var controller = new BenchmarksController(_engine);
        var result = controller.Results();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BenchmarkStatusViewModel>(viewResult.Model);

        Assert.Contains("withdrawn", model.Status);
        Assert.NotEmpty(model.Measurements);
        Assert.All(model.Measurements, m => Assert.False(string.IsNullOrEmpty(m.Measured)));
        Assert.Contains(model.NextSteps, s => s.Contains("AutoMapper 16.3.0"));
    }

    [Fact]
    public void BenchmarksController_NativeAot_ListsGeneratedMaps()
    {
        var controller = new BenchmarksController(_engine);
        var result = controller.NativeAot();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<NativeAotStatusViewModel>(viewResult.Model);

        Assert.True(model.AutoMapSharpSupportsNativeAot);
        Assert.NotEmpty(model.Plans);
        var generatedDisabled = Environment.GetEnvironmentVariable("AUTOMAPSHARP_DISABLE_GENERATED") == "1";
        Assert.True(generatedDisabled ? model.GeneratedCount == 0 : model.GeneratedCount > 0,
            "The sample app should have maps the source generator emits, unless generated code is disabled.");
        Assert.All(model.Plans.Where(p => p.Kind != "Generated"), p => Assert.False(string.IsNullOrEmpty(p.Reason)));
    }

    [Fact]
    public void AboutController_RuntimeMatrix_ReturnsTfmsAndAssemblyMetadata()
    {
        var controller = new AboutController();
        var result = controller.RuntimeMatrix();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RuntimeMatrixViewModel>(viewResult.Model);

        Assert.False(string.IsNullOrEmpty(model.CurrentFramework));
        Assert.False(string.IsNullOrEmpty(model.AutoMapSharpVersion));
        Assert.Equal(3, model.SupportedFrameworks.Count);
        Assert.Contains(model.SupportedFrameworks, t => t.Tfm == "netstandard2.0");
        Assert.Contains(model.SupportedFrameworks, t => t.Tfm == "net8.0");
        Assert.Contains(model.SupportedFrameworks, t => t.Tfm == "net9.0");
    }

    [Fact]
    public void AboutController_Pipeline_ReturnsMultiOsVerificationStages()
    {
        var controller = new AboutController();
        var result = controller.Pipeline();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PipelineViewModel>(viewResult.Model);

        Assert.Equal("GitHub Actions", model.CiProvider);
        Assert.Equal(3, model.Platforms.Count);
        Assert.True(model.VerificationStages.Count >= 8);
    }
}
