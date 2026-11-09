# Custom Property Resolvers (`IPropertyResolver`)

AutoMapSharp provides `IPropertyResolver<TSource, TDestination, TDestMember>` to enable complex property-level calculation logic that requires access to the runtime `ResolutionContext`, existing destination object state, or external services injected via the dependency injection container (`IServiceProvider`).

---

## 1. Defining a Custom Resolver

Implement `IPropertyResolver<TSource, TDestination, TDestMember>`:

```csharp
using System;
using AutoMapSharp;

public class OrderTaxResolver : IPropertyResolver<OrderSource, OrderDto, decimal>
{
    private readonly ITaxRateService _taxService;

    // Injected via ASP.NET Core / Microsoft.Extensions.DependencyInjection
    public OrderTaxResolver(ITaxRateService taxService)
    {
        _taxService = taxService;
    }

    public decimal Resolve(OrderSource source, OrderDto destination, ResolutionContext context)
    {
        decimal rate = _taxService.GetStateTaxRate(source.ShippingState);
        return Math.Round(source.Subtotal * rate, 2);
    }
}
```

---

## 2. Registering with the Fluent API

Use `.MapWith<TResolver>()` inside `.ForProperty(...)`:

```csharp
using AutoMapSharp;

public class BillingModule : MappingModule
{
    public BillingModule()
    {
        DefineMap<OrderSource, OrderDto>()
            .ForProperty(d => d.CalculatedTax, opt => opt.MapWith<OrderTaxResolver>());
    }
}
```

---

## 3. Dependency Injection Configuration

Register the services your resolver needs. Assembly scanning registers the resolver itself:

```csharp
builder.Services.AddTransient<ITaxRateService, TaxRateService>();
builder.Services.AddAutoMapSharp(typeof(Program).Assembly);
```

The engine creates `OrderTaxResolver` from the current scope for each map, so it can depend on scoped services. A resolver that is not registered is created with its parameterless constructor, or with constructor parameters supplied by the container. See [Dependency Injection](dependency-injection.md).

---

## 4. AutoMapper Migration Guide

| AutoMapper Concept | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `IValueResolver<TSource, TDestination, TDestMember>` | `IPropertyResolver<TSource, TDestination, TDestMember>` | Drop-in equivalent signature |
| `.ForMember(d => d.Tax, opt => opt.MapFrom<TaxResolver>())` | `.ForProperty(d => d.Tax, opt => opt.MapWith<TaxResolver>())` | Strongly typed and validated |
| Resolvers registered in DI | Resolvers registered in DI (`AddTransient`) | Exact same DI registration pattern |
| `ResolutionContext.Items` | `ResolutionContext.Items` | Supported for cross-operation state passing |
