using System;
using System.Collections.Generic;

namespace AutoMapSharp.Sample.Web.Models;

// ==========================================
// 5.1: Custom Property Resolvers
// ==========================================

public interface ITaxRateService
{
    decimal GetStateTaxRate(string state);
}

public class MockTaxRateService : ITaxRateService
{
    public decimal GetStateTaxRate(string state) => state switch
    {
        "CA" => 0.0825m,
        "NY" => 0.08875m,
        "TX" => 0.0625m,
        "WA" => 0.065m,
        _ => 0.05m
    };
}

public class WebOrderTaxSource
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public string ShippingState { get; set; } = "CA";
}

public class WebOrderTaxViewModel
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public string ShippingState { get; set; } = string.Empty;
    public decimal TaxRatePercent { get; set; }
    public decimal CalculatedTax { get; set; }
    public decimal TotalAmount { get; set; }
}

public class WebTaxResolver : IPropertyResolver<WebOrderTaxSource, WebOrderTaxViewModel, decimal>
{
    private readonly ITaxRateService _taxRateService;

    public WebTaxResolver(ITaxRateService taxRateService)
    {
        _taxRateService = taxRateService;
    }

    public decimal Resolve(WebOrderTaxSource source, WebOrderTaxViewModel destination, ResolutionContext context)
    {
        var rate = _taxRateService.GetStateTaxRate(source.ShippingState);
        return Math.Round(source.Subtotal * rate, 2);
    }
}

// ==========================================
// 5.2: Type-Level Converters
// ==========================================

public class WebRawJsonSource
{
    public string DocumentId { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = string.Empty;
}

public class WebParsedJsonViewModel
{
    public string DocumentId { get; set; } = string.Empty;
    public string ClusterName { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public string Region { get; set; } = string.Empty;
    public bool HighAvailability { get; set; }
}

// ==========================================
// 5.3: Polymorphic & Subclass Mapping
// ==========================================

public abstract class WebPayment
{
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ProcessedAt { get; set; }
}

public class WebCreditCardPayment : WebPayment
{
    public string CardHolder { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CardNetwork { get; set; } = "Visa";
}

public class WebCryptoPayment : WebPayment
{
    public string WalletAddress { get; set; } = string.Empty;
    public string BlockchainNetwork { get; set; } = "Ethereum";
    public decimal GasFee { get; set; }
}

public class WebBankTransferPayment : WebPayment
{
    public string BankName { get; set; } = string.Empty;
    public string RoutingNumberMasked { get; set; } = string.Empty;
}

public abstract class WebPaymentViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public abstract string PaymentType { get; }
}

public class WebCreditCardPaymentViewModel : WebPaymentViewModel
{
    public override string PaymentType => "Credit Card";
    public string CardHolder { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CardNetwork { get; set; } = string.Empty;
}

public class WebCryptoPaymentViewModel : WebPaymentViewModel
{
    public override string PaymentType => "Cryptocurrency";
    public string WalletAddress { get; set; } = string.Empty;
    public string BlockchainNetwork { get; set; } = string.Empty;
    public decimal GasFee { get; set; }
}

public class WebBankTransferPaymentViewModel : WebPaymentViewModel
{
    public override string PaymentType => "Bank Wire";
    public string BankName { get; set; } = string.Empty;
    public string RoutingNumberMasked { get; set; } = string.Empty;
}

// ==========================================
// 5.4: Lifecycle Callbacks
// ==========================================

public class WebAuditedEntity
{
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string ConfidentialCode { get; set; } = string.Empty;
}

public class WebAuditedViewModel
{
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string MaskedSecurityCode { get; set; } = string.Empty;
    public string AuditTrail { get; set; } = string.Empty;
    public DateTime MappedTimestampUtc { get; set; }
}
