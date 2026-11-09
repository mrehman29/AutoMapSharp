using AutoMapSharp;
using AutoMapSharp.Conventions;
using AutoMapSharp.Sample.Web.Models;

namespace AutoMapSharp.Sample.Web.Modules;

public class OrderWebModule : MappingModule
{
    public OrderWebModule()
    {
        // 1. Order -> OrderSummaryViewModel
        // Demonstrates case-insensitive matching, deep flattening (Customer.Address.City),
        // getter methods (GetSpecialInstructions()), enum to string, guid to string, date conversions.
        DefineMap<Order, OrderSummaryViewModel>();

        // 2. UpdateOrderViewModel -> Order
        // Demonstrates mapping into existing destination instances and assigning private setters.
        DefineMap<UpdateOrderViewModel, Order>()
            .ForProperty(d => d.Id, opt => opt.Skip())
            .ForProperty(d => d.Customer, opt => opt.Skip())
            .ForProperty(d => d.TrackingCode, opt => opt.Skip())
            .ForProperty(d => d.OrderDate, opt => opt.Skip());
    }
}

public class ExternalUserWebModule : MappingModule
{
    public ExternalUserWebModule()
    {
        // Demonstrates Cross-casing convention: snake_case source to PascalCase destination.
        SourceNamingConvention = SnakeCaseNamingConvention.Instance;
        DestinationNamingConvention = PascalCaseNamingConvention.Instance;

        DefineMap<ExternalWebhookUser, InternalUserViewModel>();
    }
}

public class LegacyCatalogWebModule : MappingModule
{
    public LegacyCatalogWebModule()
    {
        // Demonstrates prefix stripping ("m_") and suffix stripping ("Model").
        RecognizePrefixes("m_");
        RecognizeSuffixes("Model");

        DefineMap<LegacyProduct, ProductViewModel>();
    }
}

public class CustomerRecordWebModule : MappingModule
{
    public CustomerRecordWebModule()
    {
        // Demonstrates mapping into C# 9+ positional records with primary constructors.
        DefineMap<WebCustomerSource, WebCustomerRecord>();
    }
}

public class FluentWebModule : MappingModule
{
    public FluentWebModule()
    {
        // Global string trimmer across this module
        ValueTransformers.Add<string>(val => val != null ? val.Trim() : string.Empty);

        // 1. UserProfileEntity -> UserProfileViewModel
        DefineMap<UserProfileEntity, UserProfileViewModel>()
            .ForProperty(dest => dest.PasswordHash, opt => opt.Skip())
            .ForProperty(dest => dest.InternalSecurityNotes, opt => opt.Skip())
            .ForProperty(dest => dest.TenantBadge, opt => opt.UseValue("CORP-GLOBAL"))
            .ForProperty(dest => dest.DisplayName, opt => opt.MapSource(src => src.FirstName + " " + src.LastName))
            .ForProperty(dest => dest.FullAddress, opt => opt.MapSource(src => src.Street + ", " + src.City + " " + src.PostalCode))
            .ForProperty(dest => dest.EligibleForDiscount, opt => opt.MapSource(src => src.Age >= 18))
            .ForProperty(dest => dest.DiscountTier, opt => opt.If(src => src.Age >= 18).UseValue("Standard VIP"))
            .ForProperty(dest => dest.Bio, opt => opt.Fallback("No public biography provided."));

        // 2. Condition form mapping
        DefineMap<ConditionFormModel, ConditionResultViewModel>()
            .ForProperty(dest => dest.MemberStatus, opt => opt.If(src => src.AnnualSpend >= 1000m).UseValue("Gold Premium Member"))
            .ForProperty(dest => dest.Bio, opt => opt.Fallback("Default bio: Passionate shopper."));

        // 3. Bidirectional Reverse Mapping
        DefineMap<ReverseEmployeeEntity, ReverseEmployeeDto>()
            .ForProperty(dest => dest.DisplayName, opt => opt.MapSource(src => src.FullName))
            .ForProperty(dest => dest.ConfidentialCode, opt => opt.Skip())
            .ReverseMap()
            .ForProperty(dest => dest.ConfidentialCode, opt => opt.UseValue("REVERSE-RESTORED"));

        // 4. DirtyTextEntity -> CleanTextViewModel with ValueTransformers
        DefineMap<DirtyTextEntity, CleanTextViewModel>();
    }
}

public class Phase4WebModule : MappingModule
{
    public Phase4WebModule()
    {
        // 1. Order item & Multi-item order
        DefineMap<WebOrderItem, WebOrderItemViewModel>();
        DefineMap<WebMultiItemOrder, WebMultiItemOrderViewModel>();

        // 2. Custom constructor via ConstructUsing
        DefineMap<WebProductSource, WebImmutableProduct>()
            .ConstructUsing(src => new WebImmutableProduct(
                src.Sku.ToUpperInvariant(),
                src.Cost * 1.35m,
                src.IsEnterprise ? "ENTERPRISE-TIER" : "STANDARD-TIER"));

        // 3. Deep 4-level hierarchy
        DefineMap<WebOrgEnterprise, WebOrgEnterpriseViewModel>();
        DefineMap<WebOrgDivision, WebOrgDivisionViewModel>();
        DefineMap<WebOrgDepartment, WebOrgDepartmentViewModel>();
        DefineMap<WebOrgManager, WebOrgManagerViewModel>();

        // 4. Circular reference with PreserveReferences & MaxDepth
        DefineMap<WebCircularEmployee, WebCircularEmployeeViewModel>()
            .PreserveReferences(true)
            .MaxDepth(4);
    }
}

public class Phase5WebModule : MappingModule
{
    public Phase5WebModule()
    {
        DefineMap<WebOrderTaxSource, WebOrderTaxViewModel>()
            .ForProperty(d => d.CalculatedTax, opt => opt.MapWith<WebTaxResolver>())
            .ForProperty(d => d.TaxRatePercent, opt => opt.MapSource(s => s.ShippingState == "CA" ? 8.25m : 5.0m))
            .ForProperty(d => d.TotalAmount, opt => opt.MapSource(s => Math.Round(s.Subtotal + (s.Subtotal * (s.ShippingState == "CA" ? 0.0825m : 0.05m)), 2)));

        DefineMap<WebRawJsonSource, WebParsedJsonViewModel>()
            .ConvertUsing((src, ctx) =>
            {
                return new WebParsedJsonViewModel
                {
                    DocumentId = src.DocumentId,
                    ClusterName = "Production-Kube-US-West",
                    NodeCount = 32,
                    Region = "us-west-2",
                    HighAvailability = true
                };
            });

        DefineMap<WebPayment, WebPaymentViewModel>()
            .Include<WebCreditCardPayment, WebCreditCardPaymentViewModel>()
            .Include<WebCryptoPayment, WebCryptoPaymentViewModel>()
            .Include<WebBankTransferPayment, WebBankTransferPaymentViewModel>();

        DefineMap<WebCreditCardPayment, WebCreditCardPaymentViewModel>()
            .IncludeBase<WebPayment, WebPaymentViewModel>();

        DefineMap<WebCryptoPayment, WebCryptoPaymentViewModel>()
            .IncludeBase<WebPayment, WebPaymentViewModel>();

        DefineMap<WebBankTransferPayment, WebBankTransferPaymentViewModel>()
            .IncludeBase<WebPayment, WebPaymentViewModel>();

        DefineMap<WebAuditedEntity, WebAuditedViewModel>()
            .ForProperty(d => d.MaskedSecurityCode, opt => opt.Skip())
            .ForProperty(d => d.AuditTrail, opt => opt.Skip())
            .ForProperty(d => d.MappedTimestampUtc, opt => opt.Skip())
            .BeforeMap((src, dest, ctx) =>
            {
                dest.AuditTrail = $"[AUDIT-START: Initiated mapping for {src.EntityName}]";
            })
            .AfterMap((src, dest, ctx) =>
            {
                dest.AuditTrail += " -> [AUDIT-FINISH: Completed]";
                dest.MaskedSecurityCode = "SEC-***-REDACTED";
                dest.MappedTimestampUtc = new DateTime(2026, 11, 4, 18, 0, 0, DateTimeKind.Utc);
            });
    }
}

public class Phase6WebModule : MappingModule
{
    public Phase6WebModule()
    {
        DefineMap<DbOrderItem, OrderItemProjectionDto>()
            .ForProperty(d => d.LineTotal, opt => opt.MapSource(s => s.UnitPrice * s.Quantity));

        DefineMap<DbOrder, OrderProjectionDto>()
            .ForProperty(d => d.TenantBadge, opt => opt.UseValue("CORP-ENTERPRISE-HQ"))
            .ForProperty(d => d.Items, opt => opt.ExplicitExpansion())
            .ForProperty(d => d.AuditLogs, opt => opt.ExplicitExpansion());
    }
}
