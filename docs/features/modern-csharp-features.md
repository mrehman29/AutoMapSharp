# Modern C# Features Support (C# 9 - 13)

## Overview
AutoMapSharp was engineered from the ground up to support modern C# language features, including positional records, `init`-only properties, `required` members, and Nullable Reference Types (`#nullable enable`).

## Supported Modern Features

### 1. Positional Records & Primary Constructors
No parameterless constructor is required. AutoMapSharp automatically matches constructor parameters by name (case-insensitive) to source properties:
```csharp
public record PersonDto(string FirstName, string LastName, int Age);

// Maps cleanly from PersonEntity without any extra configuration:
var dto = engine.Map<PersonEntity, PersonDto>(entity);
```

### 2. `init`-Only Properties
Properties with `init` setters are assigned during instantiation:
```csharp
public class OrderDto
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
}
```

### 3. Private and Internal Setters
Domain-driven entities frequently restrict property setters to `private` or `internal` to enforce invariants. AutoMapSharp can assign these properties directly:
```csharp
public class Account
{
    public string AccountNumber { get; private set; }
    public decimal Balance { get; private set; }
}
```

### 4. Nullable Reference Types (NRT)
All generated code includes `#nullable enable` and respects compiler annotations, avoiding CS8600, CS8602, and CS8618 warnings.

## Migrating from AutoMapper

| Feature | AutoMapper Approach | AutoMapSharp Approach |
|---|---|---|
| Records / Constructor Params | `.ForCtorParam("paramName", opt => ...)` | Automatic convention matching |
| `init` properties | Requires newer versions | First-class native support |
| Private setters | `ShouldMapProperty = p => true` | Maps by convention |
