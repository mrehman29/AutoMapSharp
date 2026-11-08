# Constructor Mapping & Records (`ConstructUsing`)

## Overview
Immutable domain types, C# 9+ positional records, and types without parameterless constructors require constructor-based instantiation.

AutoMapSharp provides two powerful approaches:
1. **Automatic Parameter Matching:** Auto-matches constructor parameters by name (case-insensitively) against source properties.
2. **Explicit `ConstructUsing`:** Takes complete control over object construction using a custom factory lambda.

## AutoMapSharp Usage

### 1. Automatic Record Constructor Matching
```csharp
public record UserSummary(int Id, string FullName, string Tier);

// In module:
DefineMap<UserEntity, UserSummary>();
// AutoMapSharp matches (Id, FullName, Tier) parameters directly from UserEntity!
```

### 2. Custom Factory with `ConstructUsing`
```csharp
public class ImmutableAccount
{
    public string AccountCode { get; }
    public decimal Balance { get; }

    public ImmutableAccount(string accountCode, decimal balance)
    {
        AccountCode = accountCode;
        Balance = balance;
    }
}

public class AccountModule : MappingModule
{
    public AccountModule()
    {
        DefineMap<AccountEntity, ImmutableAccount>()
            .ConstructUsing(src => new ImmutableAccount(src.Prefix + "-" + src.Number, src.LedgerBalance));
    }
}
```

## ForCtorParam and ConstructUsing with the context

```csharp
DefineMap<TicketSource, Ticket>()
    .ForCtorParam("code", o => o.MapSource(s => "T-" + s.Reference))
    .ForCtorParam("priority", o => o.UseValue(2));

DefineMap<Order, OrderDto>()
    .ConstructUsing((src, ctx) => new OrderDto((string)ctx.Items["currency"]!));
```

A `ForCtorParam` name that matches no constructor parameter is reported by `ValidateConfiguration`. Members are still mapped after `ConstructUsing`, so `Skip()` a member if the factory already set it.

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `opt.ConstructUsing(src => new ...)` | `opt.ConstructUsing(src => new ...)` | 100% drop-in replacement |
| `ForCtorParam("name", opt => ...)` | `ForCtorParam("name", opt => ...)` | Parameters are also matched by name automatically |

## How a constructor is chosen

1. `ConstructUsing`, when configured.
2. A parameterless constructor, public first. Members are then set one by one.
3. Otherwise, the constructor with the most parameters that can all be resolved. A parameter is resolved from a `ForProperty` rule with the same name, a source member found by name, convention or flattening, or its default value.

Constructor parameters get the same mapping as properties. A parameter of a nested record type is mapped with its own map, a collection parameter is mapped element by element, and `CustomerName` is flattened from `Customer.Name`:

```csharp
public record AddressRecord(string City, string Country);
public record OrderRecord(int Id, string CustomerName, AddressRecord ShippingAddress, IReadOnlyList<LineRecord> Lines);

DefineMap<AddressEntity, AddressRecord>();
DefineMap<LineEntity, LineRecord>();
DefineMap<OrderEntity, OrderRecord>();
```

Members that were set through the constructor are not set again afterwards. A struct with settable members starts from its default value. A struct without any, such as a `readonly struct`, is built through its constructor.

## Performance & Native AOT
- Constructor calls are compiled into the plan, with no reflection per map.
- Native AOT is not verified yet. See [Native AOT & Trimming: Current Status](native-aot-and-trimming.md).
