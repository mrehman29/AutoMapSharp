# Value Converters (`IValueConverter`)

A value converter turns one source member value into one destination member value. It does not see the whole source object, so one converter can be reused across many maps.

```csharp
public class CentsToCurrency : IValueConverter<long, string>
{
    public string Convert(long sourceMember, ResolutionContext context) =>
        (sourceMember / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}

DefineMap<Invoice, InvoiceDto>()
    // Reads the source member with the same name, Total.
    .ForProperty(d => d.Total, o => o.ConvertUsing<CentsToCurrency>())
    // Reads the given source member.
    .ForProperty(d => d.Tax, o => o.ConvertUsing<CentsToCurrency, long>(s => s.TaxCents));
```

* The source value is converted to the converter's input type first, and the result is converted to the member type.
* Converters are created from the service provider, so they can take dependencies. Assembly scanning registers them.
* `ProjectOnto` rejects a member that uses a value converter with a clear error, because the converter runs .NET code the database cannot run.

## Which one to use

| Need | Use |
|---|---|
| The value depends on several source members | `IPropertyResolver` with `MapWith` |
| The value depends on one member and the logic is reusable | `IValueConverter` with `ConvertUsing` |
| The whole object is built by custom code | `ITypeConverter` with `ConvertUsing` on the map |
| A small inline change | `MapSource` or `AddTransform` |

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `IValueConverter<TSourceMember, TDestinationMember>` | Same name. `Convert` takes (sourceMember, context) |
| `o.ConvertUsing(new CentsToCurrency())` | `o.ConvertUsing<CentsToCurrency>()` |
| `o.ConvertUsing<CentsToCurrency, long>(s => s.TaxCents)` | Same |
