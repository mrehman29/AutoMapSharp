# Property Matching Conventions

## Overview
By default, AutoMapSharp matches destination properties to source properties using case-insensitive ordinal name comparison. It supports mapping both onto new instances and existing destination objects (crucial for Entity Framework entity updates).

## AutoMapSharp Usage

```csharp
public class User
{
    public int id { get; set; }
    public string first_name { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
}

public class UserModule : MappingModule
{
    public UserModule()
    {
        // By default, properties with matching names (case-insensitive) are mapped automatically
        DefineMap<User, UserDto>();
    }
}
```

## Fields and member filters

Public fields are mapped on both sides, like properties. Read-only fields are never written.

A module can limit which members take part:

```csharp
public class OrderModule : MappingModule
{
    public OrderModule()
    {
        ShouldMapProperty = p => p.GetGetMethod()?.IsPublic == true && p.Name != "Internal";
        ShouldMapField = f => !f.Name.StartsWith("_");
        ShouldMapMethod = m => m.Name.StartsWith("Get");
        DefineMap<Order, OrderDto>();
    }
}
```

The filters apply to source and destination members, and validation uses them too.

## Migrating from AutoMapper

| AutoMapper Behavior | AutoMapSharp Behavior | Notes |
|---|---|---|
| Matches by name case-insensitively | Matches by name case-insensitively | 100% compatible |
| Requires parameterless ctor or `ForCtorParam` | Matches positional record constructors automatically | Simpler setup for records |

## Notes & Performance
* Direct properties are assigned without intermediate boxing for value types.
* Private and internal setters are assigned without requiring public exposure of domain properties.
