# Subclass & Polymorphic Mapping (`Include` & `IncludeBase`)

AutoMapSharp supports runtime polymorphic dispatch when mapping object hierarchies. When a source instance is a derived class, AutoMapSharp dispatches to the corresponding derived destination type mapping, preserving polymorphic behavior across collections and complex graphs.

---

## 1. Configuring Polymorphic Hierarchies

Given a domain inheritance structure:

```csharp
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
```

Configure base and derived mappings using `.Include<TDerivedSource, TDerivedDestination>()` and `.IncludeBase<TBaseSource, TBaseDestination>()`:

```csharp
using AutoMapSharp;

public class PaymentModule : MappingModule
{
    public PaymentModule()
    {
        // 1. Tell base map which derived pairs to dispatch to
        DefineMap<Payment, PaymentDto>()
            .Include<CreditCardPayment, CreditCardPaymentDto>()
            .Include<CryptoPayment, CryptoPaymentDto>();

        // 2. Inherit base configuration rules on derived pairs
        DefineMap<CreditCardPayment, CreditCardPaymentDto>()
            .IncludeBase<Payment, PaymentDto>();

        DefineMap<CryptoPayment, CryptoPaymentDto>()
            .IncludeBase<Payment, PaymentDto>();
    }
}
```

---

## 2. Runtime Polymorphic Dispatch

When mapping via the base interface or collection, AutoMapSharp inspects the concrete runtime type and instantiates the correct derived DTO:

```csharp
Payment payment = new CreditCardPayment
{
    Amount = 450.00m,
    Currency = "USD",
    CardNumberMasked = "************4242"
};

// Calling Map with base types returns concrete CreditCardPaymentDto:
PaymentDto dto = mapper.Map<Payment, PaymentDto>(payment);

Assert.IsType<CreditCardPaymentDto>(dto);
Assert.Equal("************4242", ((CreditCardPaymentDto)dto).CardNumberMasked);
```

When mapping lists of base types (`List<Payment>` $\rightarrow$ `List<PaymentDto>`), each item in the destination collection is instantiated as its corresponding derived DTO.

### How dispatch is chosen

For a source object whose runtime type differs from the map's source type, the engine looks in this order for:

1. An `Include` pair whose source type matches the runtime type.
2. A map defined exactly for the runtime type and the requested destination type.
3. When the destination type is abstract or an interface, the first defined map, in definition order, whose source type matches and whose destination type is concrete and assignable.

The choice is made once per runtime type and cached.

`IncludeBase` copies the base map's member rules, `ForPath` rules, `BeforeMap` and `AfterMap` callbacks, `MaxDepth` and `PreserveReferences`. Base callbacks run before the derived map's own callbacks, in the order they were declared. The base map must be defined, otherwise the engine throws when it is built.

---

### IncludeAllDerived and As

`IncludeAllDerived()` dispatches to the defined map of any derived pair, choosing the one whose source type is closest to the runtime type. The derived maps also inherit the base map's configuration, as if each called `IncludeBase`:

```csharp
DefineMap<Animal, AnimalDto>().IncludeAllDerived().ForProperty(d => d.Sound, o => o.UseValue("generic"));
DefineMap<Dog, DogDto>();
DefineMap<Puppy, PuppyDto>();
```

`As<TOther>()` maps to a more specific destination type with that pair's map:

```csharp
DefineMap<Vehicle, VehicleDto>().As<CarDto>();
DefineMap<Vehicle, CarDto>();
```

A map can call `IncludeBase` several times. The bases are merged in order, and the map's own configuration wins.

---

## 3. AutoMapper Migration Guide

| AutoMapper Syntax | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `CreateMap<Base, BaseDto>().Include<Sub, SubDto>()` | `DefineMap<Base, BaseDto>().Include<Sub, SubDto>()` | Dispatches derived source to derived dest |
| `CreateMap<Sub, SubDto>().IncludeBase<Base, BaseDto>()` | `DefineMap<Sub, SubDto>().IncludeBase<Base, BaseDto>()` | Inherits base member rules and actions |
| Heterogeneous Collection Mapping | Heterogeneous Collection Mapping | Full support across `List<T>`, arrays, and sets |
