# Enum Mapping

Enums convert to and from strings, numbers and other enums without configuration.

| From | To | Rule |
|---|---|---|
| Enum | Another enum | By member name, ignoring case. A value with no matching name, or a combination of flags, uses its numeric value |
| Enum | `string` | The member name, from `ToString()` |
| `string` | Enum | `Enum.Parse`, ignoring case. An unknown name throws an `AutoMapSharpMappingException` that names the member |
| Enum | Number | The underlying numeric value |
| Number | Enum | The enum value with that number |

Nullable enums follow the same rules, and `null` stays `null`.

## Example

```csharp
public enum LegacyPriority { Low, Medium, High, Critical, Escalated }
public enum TicketPriority { Critical, High, Medium, Low, Unknown }

DefineMap<LegacyTicket, TicketView>();
```

| `LegacyPriority` | `TicketPriority` |
|---|---|
| `Low` (0) | `Low` (3) |
| `Critical` (3) | `Critical` (0) |
| `Escalated` (4) | `Unknown` (4), by numeric value because no name matches |

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| Enum to enum by name, then by value | Same |
| `ConvertUsing` for custom enum rules | `ConvertUsing` on `DefineMap<TEnumA, TEnumB>()` |

## See it running

The sample web app has an **Enum Mapping by Name** page under **Reliability & Parity**.
