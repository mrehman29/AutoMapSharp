using System;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

public class OrdersController : Controller
{
    private readonly IMapEngine _mapper;

    public OrdersController(IMapEngine mapper)
    {
        _mapper = mapper;
    }

    // Demonstrates: New instance mapping, deep flattening, enum/guid/date conversions, getter methods
    public IActionResult Index(int id = 1001)
    {
        var order = MockDatabase.GetOrder(id) ?? MockDatabase.GetOrder(1001)!;
        var viewModel = _mapper.Map<Order, OrderSummaryViewModel>(order);

        var comparison = new ComparisonViewModel<Order, OrderSummaryViewModel>
        {
            FeatureTitle = "Deep Object Flattening & Built-in Type Conversions",
            FeatureDescription = "AutoMapSharp automatically resolves nested paths (Customer.Address.City -> CustomerAddressCity), invokes parameterless getter methods (GetSpecialInstructions() -> SpecialInstructions), converts Enums to Strings, Guids to Strings, and DateTime to DateTimeOffset.",
            MappingRuleCode = "DefineMap<Order, OrderSummaryViewModel>();",
            Source = order,
            Destination = viewModel
        };

        return View(comparison);
    }

    // Demonstrates: Existing destination instance update in-place (EF Core update pattern)
    [HttpGet]
    public IActionResult Update(int id = 1001)
    {
        var order = MockDatabase.GetOrder(id) ?? MockDatabase.GetOrder(1001)!;
        var request = new UpdateOrderViewModel
        {
            OrderId = order.Id,
            TotalAmount = 2499.50m,
            Status = OrderStatus.Processing
        };

        var comparison = new ComparisonViewModel<UpdateOrderViewModel, Order>
        {
            FeatureTitle = "In-Place Existing Destination Instance Update",
            FeatureDescription = "Updates an existing tracked entity without allocating a new object reference, preserving EF Core entity tracking and updating private setters directly.",
            MappingRuleCode = "_mapper.Map(request, existingOrder);",
            Source = request,
            Destination = order,
            IsInPlaceUpdate = true
        };

        return View(comparison);
    }

    [HttpPost]
    public IActionResult ApplyUpdate(UpdateOrderViewModel request)
    {
        var existingOrder = MockDatabase.GetOrder(request.OrderId);
        if (existingOrder == null)
        {
            return NotFound();
        }

        // Perform in-place update
        var updated = _mapper.Map(request, existingOrder);

        var comparison = new ComparisonViewModel<UpdateOrderViewModel, Order>
        {
            FeatureTitle = "In-Place Existing Destination Instance Update (Applied)",
            FeatureDescription = "The existing order instance was mutated directly in memory. Reference equality test proves the original object was updated without re-instantiation.",
            MappingRuleCode = "_mapper.Map(request, existingOrder);",
            Source = request,
            Destination = updated,
            IsInPlaceUpdate = true,
            ReferenceEqual = ReferenceEquals(existingOrder, updated)
        };

        return View("Update", comparison);
    }

    // Demonstrates: Safe short-circuiting when intermediate child objects are null
    public IActionResult NullSafe()
    {
        var orderWithNulls = MockDatabase.GetOrder(1003)!;
        var viewModel = _mapper.Map<Order, OrderSummaryViewModel>(orderWithNulls);

        var comparison = new ComparisonViewModel<Order, OrderSummaryViewModel>
        {
            FeatureTitle = "Null-Safe Deep Flattening (Zero Exceptions)",
            FeatureDescription = "The Order has Customer = null. AutoMapSharp automatically short-circuits the evaluation and sets CustomerName and CustomerAddressCity to null without throwing a NullReferenceException.",
            MappingRuleCode = "DefineMap<Order, OrderSummaryViewModel>(); // Automatic null-propagation",
            Source = orderWithNulls,
            Destination = viewModel
        };

        return View(comparison);
    }
}

public class ConventionsController : Controller
{
    private readonly IMapEngine _mapper;

    public ConventionsController(IMapEngine mapper)
    {
        _mapper = mapper;
    }

    // Demonstrates: snake_case to PascalCase convention mapping
    public IActionResult SnakeCase()
    {
        var webhookPayload = MockDatabase.GetSampleWebhookUser();
        var userViewModel = _mapper.Map<ExternalWebhookUser, InternalUserViewModel>(webhookPayload);

        var comparison = new ComparisonViewModel<ExternalWebhookUser, InternalUserViewModel>
        {
            FeatureTitle = "Naming Conventions (snake_case -> PascalCase)",
            FeatureDescription = "AutoMapSharp normalizes words across naming conventions, seamlessly matching first_name to FirstName, last_name to LastName, and user_age to UserAge.",
            MappingRuleCode = "SourceNamingConvention = SnakeCaseNamingConvention.Instance;\nDestinationNamingConvention = PascalCaseNamingConvention.Instance;\nDefineMap<ExternalWebhookUser, InternalUserViewModel>();",
            Source = webhookPayload,
            Destination = userViewModel
        };

        return View(comparison);
    }

    // Demonstrates: Prefix ("m_") and Suffix ("Model") stripping
    public IActionResult PrefixSuffix()
    {
        var legacyProduct = MockDatabase.GetSampleLegacyProduct();
        var productDto = _mapper.Map<LegacyProduct, ProductViewModel>(legacyProduct);

        var comparison = new ComparisonViewModel<LegacyProduct, ProductViewModel>
        {
            FeatureTitle = "Member Prefix & Suffix Stripping",
            FeatureDescription = "AutoMapSharp strips Hungarian prefixes (e.g. 'm_Title' -> 'Title', 'm_Price' -> 'Price') and architectural suffixes (e.g. 'CategoryModel' -> 'Category') during matching.",
            MappingRuleCode = "RecognizePrefixes(\"m_\");\nRecognizeSuffixes(\"Model\");\nDefineMap<LegacyProduct, ProductViewModel>();",
            Source = legacyProduct,
            Destination = productDto
        };

        return View(comparison);
    }
}

public class RecordsController : Controller
{
    private readonly IMapEngine _mapper;

    public RecordsController(IMapEngine mapper)
    {
        _mapper = mapper;
    }

    // Demonstrates: Mapping into modern C# 9+ positional record
    public IActionResult Index()
    {
        var source = MockDatabase.GetSampleCustomerRecordSource();
        var record = _mapper.Map<WebCustomerSource, WebCustomerRecord>(source);

        var comparison = new ComparisonViewModel<WebCustomerSource, WebCustomerRecord>
        {
            FeatureTitle = "Modern C# 9+ Positional Record Mapping",
            FeatureDescription = "Positional records have no parameterless constructor. AutoMapSharp inspects the primary constructor arguments (Id, FullName, Tier), matches them case-insensitively to source properties, and instantiates the immutable record.",
            MappingRuleCode = "public record WebCustomerRecord(int Id, string FullName, string Tier);\nDefineMap<WebCustomerSource, WebCustomerRecord>();",
            Source = source,
            Destination = record
        };

        return View(comparison);
    }
}

public class DynamicController : Controller
{
    private readonly IMapEngine _mapper;

    public DynamicController(IMapEngine mapper)
    {
        _mapper = mapper;
    }

    // Demonstrates: Untyped object mapping and dynamic runtime reflection mapping
    public IActionResult Index()
    {
        object untypedSource = MockDatabase.GetSampleCustomerRecordSource();

        // 1. Untyped source mapping: _mapper.Map<TDestination>(object source)
        var untypedMapped = _mapper.Map<WebCustomerRecord>(untypedSource);

        // 2. Full dynamic runtime reflection mapping: _mapper.Map(object, Type, Type)
        var reflectionMapped = _mapper.Map(untypedSource, typeof(WebCustomerSource), typeof(WebCustomerRecord));

        var comparison = new ComparisonViewModel<object, WebCustomerRecord>
        {
            FeatureTitle = "Untyped & Dynamic Runtime Reflection Mapping",
            FeatureDescription = "When source types are only known as 'object' at runtime (e.g. MediatR handlers, controllers, dynamic dispatch), AutoMapSharp dynamically resolves runtime types and dispatches to compiled delegates.",
            MappingRuleCode = "object untypedSource = GetObject();\nvar record = _mapper.Map<WebCustomerRecord>(untypedSource);\nvar reflection = _mapper.Map(untypedSource, typeof(WebCustomerSource), typeof(WebCustomerRecord));",
            Source = untypedSource,
            Destination = untypedMapped
        };

        return View(comparison);
    }
}
