using System;
using System.Collections.Generic;
using AutoMapSharp.Configuration;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase5Tests
{
    // ==========================================
    // 5.1: Custom Property Resolvers
    // ==========================================

    public class OrderSource
    {
        public decimal Subtotal { get; set; }
        public string ShippingState { get; set; } = "CA";
    }

    public class OrderDto
    {
        public decimal Subtotal { get; set; }
        public decimal CalculatedTax { get; set; }
        public decimal TotalWithTax { get; set; }
    }

    public interface ITaxService
    {
        decimal GetTaxRate(string state);
    }

    public class MockTaxService : ITaxService
    {
        public decimal GetTaxRate(string state) => state == "CA" ? 0.0825m : 0.05m;
    }

    public class OrderTaxResolver : IPropertyResolver<OrderSource, OrderDto, decimal>
    {
        private readonly ITaxService _taxService;

        public OrderTaxResolver(ITaxService taxService)
        {
            _taxService = taxService;
        }

        public decimal Resolve(OrderSource source, OrderDto destination, ResolutionContext context)
        {
            return Math.Round(source.Subtotal * _taxService.GetTaxRate(source.ShippingState), 2);
        }
    }

    public class OrderTotalResolver : IPropertyResolver<OrderSource, OrderDto, decimal>
    {
        public decimal Resolve(OrderSource source, OrderDto destination, ResolutionContext context)
        {
            // Destination already has Subtotal and CalculatedTax mapped or calculated
            return destination.Subtotal + destination.CalculatedTax;
        }
    }

    public class ResolverModule : MappingModule
    {
        public ResolverModule()
        {
            DefineMap<OrderSource, OrderDto>()
                .ForProperty(d => d.CalculatedTax, opt => opt.MapWith<OrderTaxResolver>())
                .ForProperty(d => d.TotalWithTax, opt => opt.MapWith<OrderTotalResolver>());
        }
    }

    [Fact]
    public void Task5_1_CustomPropertyResolver_WithInjectedService_ResolvesValue()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxService, MockTaxService>();
        services.AddTransient<OrderTaxResolver>();
        services.AddTransient<OrderTotalResolver>();
        var provider = services.BuildServiceProvider();

        var engine = new MapEngine(new[] { new ResolverModule() }, provider);

        var source = new OrderSource { Subtotal = 100.00m, ShippingState = "CA" };
        var result = engine.Map<OrderSource, OrderDto>(source);

        Assert.Equal(100.00m, result.Subtotal);
        Assert.Equal(8.25m, result.CalculatedTax);
        Assert.Equal(108.25m, result.TotalWithTax);
    }

    // ==========================================
    // 5.2: Type-Level Converters
    // ==========================================

    public class JsonStringPayload
    {
        public string RawJson { get; set; } = "{\"name\":\"SuperWidget\",\"count\":42}";
    }

    public class ParsedPayloadDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class JsonPayloadConverter : ITypeConverter<JsonStringPayload, ParsedPayloadDto>
    {
        public ParsedPayloadDto Convert(JsonStringPayload source, ResolutionContext context)
        {
            // Simple custom parsing simulation
            var name = source.RawJson.Contains("\"name\":\"SuperWidget\"") ? "SuperWidget" : "Unknown";
            var count = source.RawJson.Contains("\"count\":42") ? 42 : 0;
            return new ParsedPayloadDto { Name = name, Count = count };
        }
    }

    public class TypeConverterModule : MappingModule
    {
        public TypeConverterModule()
        {
            DefineMap<JsonStringPayload, ParsedPayloadDto>()
                .ConvertUsing<JsonPayloadConverter>();

            DefineMap<string, int>()
                .ConvertUsing((str, ctx) => int.TryParse(str, out var v) ? v : -1);
        }
    }

    [Fact]
    public void Task5_2_TypeLevelConverter_ClassAndLambda_BypassesStandardMemberGeneration()
    {
        var engine = new MapEngine(new[] { new TypeConverterModule() });

        // Class converter
        var source = new JsonStringPayload();
        var result = engine.Map<JsonStringPayload, ParsedPayloadDto>(source);

        Assert.Equal("SuperWidget", result.Name);
        Assert.Equal(42, result.Count);

        // Lambda converter
        var num = engine.Map<string, int>("12345");
        Assert.Equal(12345, num);

        var invalidNum = engine.Map<string, int>("not-a-number");
        Assert.Equal(-1, invalidNum);
    }

    // ==========================================
    // 5.3: Subclass & Polymorphic Mapping (Include & IncludeBase)
    // ==========================================

    public abstract class Payment
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class CreditCardPayment : Payment
    {
        public string CardNumberMasked { get; set; } = string.Empty;
    }

    public class CryptoPayment : Payment
    {
        public string WalletAddress { get; set; } = string.Empty;
    }

    public abstract class PaymentDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public class CreditCardPaymentDto : PaymentDto
    {
        public string CardNumberMasked { get; set; } = string.Empty;
    }

    public class CryptoPaymentDto : PaymentDto
    {
        public string WalletAddress { get; set; } = string.Empty;
    }

    public class PolymorphicModule : MappingModule
    {
        public PolymorphicModule()
        {
            DefineMap<Payment, PaymentDto>()
                .Include<CreditCardPayment, CreditCardPaymentDto>()
                .Include<CryptoPayment, CryptoPaymentDto>();

            DefineMap<CreditCardPayment, CreditCardPaymentDto>()
                .IncludeBase<Payment, PaymentDto>();

            DefineMap<CryptoPayment, CryptoPaymentDto>()
                .IncludeBase<Payment, PaymentDto>();
        }
    }

    [Fact]
    public void Task5_3_PolymorphicSubclassMapping_DispatchesToDerivedType()
    {
        var engine = new MapEngine(new[] { new PolymorphicModule() });

        Payment ccPayment = new CreditCardPayment
        {
            Amount = 250.00m,
            Currency = "USD",
            CardNumberMasked = "************4242"
        };

        Payment cryptoPayment = new CryptoPayment
        {
            Amount = 1.5m,
            Currency = "ETH",
            WalletAddress = "0x71C...8976"
        };

        var mappedCc = engine.Map<Payment, PaymentDto>(ccPayment);
        var mappedCrypto = engine.Map<Payment, PaymentDto>(cryptoPayment);

        var ccDto = Assert.IsType<CreditCardPaymentDto>(mappedCc);
        Assert.Equal(250.00m, ccDto.Amount);
        Assert.Equal("USD", ccDto.Currency);
        Assert.Equal("************4242", ccDto.CardNumberMasked);

        var cryptoDto = Assert.IsType<CryptoPaymentDto>(mappedCrypto);
        Assert.Equal(1.5m, cryptoDto.Amount);
        Assert.Equal("ETH", cryptoDto.Currency);
        Assert.Equal("0x71C...8976", cryptoDto.WalletAddress);
    }

    // ==========================================
    // 5.4: Lifecycle Callbacks (BeforeMap, AfterMap)
    // ==========================================

    public class AuditSource
    {
        public string RawValue { get; set; } = "HelloWorld";
    }

    public class AuditDto
    {
        public string ProcessedValue { get; set; } = string.Empty;
        public string TraceLog { get; set; } = string.Empty;
        public DateTime MappedAtUtc { get; set; }
    }

    public class LifecycleModule : MappingModule
    {
        public LifecycleModule()
        {
            DefineMap<AuditSource, AuditDto>()
                .ForProperty(d => d.ProcessedValue, opt => opt.MapSource(s => s.RawValue))
                .BeforeMap((src, dest, ctx) =>
                {
                    dest.TraceLog = "Started:";
                })
                .AfterMap((src, dest, ctx) =>
                {
                    dest.TraceLog += "Finished";
                    dest.MappedAtUtc = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
                });
        }
    }

    [Fact]
    public void Task5_4_LifecycleCallbacks_ExecuteBeforeAndAfterAssignments()
    {
        var engine = new MapEngine(new[] { new LifecycleModule() });

        var src = new AuditSource { RawValue = "AutoMapSharp-V1" };
        var result = engine.Map<AuditSource, AuditDto>(src);

        Assert.Equal("AutoMapSharp-V1", result.ProcessedValue);
        Assert.Equal("Started:Finished", result.TraceLog);
        Assert.Equal(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc), result.MappedAtUtc);
    }

    // ==========================================
    // 5.5: Configuration Validation (AssertConfigurationIsValid)
    // ==========================================

    public class IncompleteSource
    {
        public string Title { get; set; } = string.Empty;
    }

    public class IncompleteDto
    {
        public string Title { get; set; } = string.Empty;
        public string UnmappedCode { get; set; } = string.Empty;
        public int UnmappedNumber { get; set; }
    }

    public class CompleteDto
    {
        public string Title { get; set; } = string.Empty;
        public string ExplicitlySkipped { get; set; } = string.Empty;
    }

    public class ValidModule : MappingModule
    {
        public ValidModule()
        {
            DefineMap<IncompleteSource, CompleteDto>()
                .ForProperty(d => d.ExplicitlySkipped, opt => opt.Skip());
        }
    }

    public class InvalidModule : MappingModule
    {
        public InvalidModule()
        {
            DefineMap<IncompleteSource, IncompleteDto>();
        }
    }

    [Fact]
    public void Task5_5_ConfigurationValidation_ValidConfig_PassesWithoutErrors()
    {
        var engine = new MapEngine(new[] { new ValidModule() });
        var validation = engine.ValidateConfiguration();

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
        engine.AssertConfigurationIsValid(); // Should not throw
    }

    [Fact]
    public void Task5_5_ConfigurationValidation_UnmappedMembers_ThrowsConfigurationException()
    {
        var engine = new MapEngine(new[] { new InvalidModule() });
        var validation = engine.ValidateConfiguration();

        Assert.False(validation.IsValid);
        Assert.Single(validation.Errors);

        var error = validation.Errors[0];
        Assert.Equal(typeof(IncompleteSource), error.SourceType);
        Assert.Equal(typeof(IncompleteDto), error.DestinationType);
        Assert.Contains("UnmappedCode", error.UnmappedProperties);
        Assert.Contains("UnmappedNumber", error.UnmappedProperties);

        var ex = Assert.Throws<AutoMapSharpConfigurationException>(() => engine.AssertConfigurationIsValid());
        Assert.Contains("UnmappedCode", ex.Message);
        Assert.Contains("UnmappedNumber", ex.Message);
        Assert.Contains("AutoMapSharp Configuration Validation Failed", ex.Message);
    }
}
