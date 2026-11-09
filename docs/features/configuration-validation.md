# Configuration Validation (`AssertConfigurationIsValid`)

Configuration drift between domain models and DTOs is a frequent source of production defects. AutoMapSharp provides build-time and unit-test validation through `engine.AssertConfigurationIsValid()` and `engine.ValidateConfiguration()`.

---

## 1. Verifying Configurations in Unit Tests

Add a unit test in your test suite that asserts the validity of all registered modules:

```csharp
using AutoMapSharp;
using Xunit;

public class MappingConfigurationTests
{
    [Fact]
    public void AutoMapSharp_Configuration_IsValid()
    {
        var engine = new MapEngine(new MappingModule[]
        {
            new OrderMappingModule(),
            new UserMappingModule()
        });

        // Throws AutoMapSharpConfigurationException if any destination property is unmapped
        engine.AssertConfigurationIsValid();
    }
}
```

---

## 2. Non-Throwing Diagnostic Reports

To inspect mapping configuration health in monitoring endpoints or application dashboards:

```csharp
var result = engine.ValidateConfiguration();

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Unmapped in {error.DestinationType.Name}: {string.Join(", ", error.UnmappedProperties)}");
    }

    string fullReport = result.GenerateReport();
}
```

---

## 3. Resolving Validation Errors

If a destination property is not mapped by convention, direct name match, or flattening, resolve the error using one of the following strategies:

1. **Explicitly Skip**:
   ```csharp
   .ForProperty(d => d.InternalSecurityStamp, opt => opt.Skip())
   ```
2. **Assign Constant**:
   ```csharp
   .ForProperty(d => d.TenantId, opt => opt.UseValue("CORP-GLOBAL"))
   ```
3. **Map Expression**:
   ```csharp
   .ForProperty(d => d.FullName, opt => opt.MapSource(s => s.FirstName + " " + s.LastName))
   ```
4. **Custom Resolver**:
   ```csharp
   .ForProperty(d => d.Tax, opt => opt.MapWith<OrderTaxResolver>())
   ```

---

## What validation checks

`ValidateConfiguration` and `AssertConfigurationIsValid` use the engine's own matching and conversion rules. They report:

* Destination members with no source, including on maps that use `ConstructUsing`.
* Members whose types cannot be converted, such as an `Address` to an `int`.
* Constructors that cannot be satisfied, and `ForCtorParam` names that match no parameter.
* `Include` pairs that have no map.
* Problems inside the automatic maps used for nested types, with the member that uses them.

Choose what a map checks with `ValidateMembers`:

```csharp
DefineMap<Order, OrderSummary>()
    .ValidateMembers(MemberList.Source)
    .ForSourceMember(s => s.InternalNotes, o => o.DoNotValidate());
```

| `MemberList` | Checks |
|---|---|
| `Destination`, the default | Every destination member has a source |
| `Source` | Every source member is used |
| `None` | No member check. Type, constructor and include checks still run |

Maps that use a type converter are skipped, because the converter builds the whole destination.

## 4. AutoMapper Migration Guide

| AutoMapper Syntax | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `config.AssertConfigurationIsValid()` | `engine.AssertConfigurationIsValid()` | Throws `AutoMapSharpConfigurationException` |
| `AutoMapperConfigurationException` | `AutoMapSharpConfigurationException` | Clear, detailed diagnostic report with suggested fixes |
| Non-throwing diagnostic check | `engine.ValidateConfiguration()` | Returns `ConfigurationValidationResult` object |
