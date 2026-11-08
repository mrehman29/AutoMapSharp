# Property Skipping & Constant Values (`Skip`, `UseValue`)

## Overview
When mapping entities to DTOs or ViewModels, sensitive properties (e.g., `PasswordHash`, `InternalAuditNotes`) often need to be omitted, while other fields should be initialized with fixed constants (e.g., `TenantId = "GLOBAL"` or static badges).

AutoMapSharp provides `opt.Skip()` and `opt.UseValue(constant)` within `.ForProperty()`.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class UserMappingModule : MappingModule
{
    public UserMappingModule()
    {
        DefineMap<UserEntity, UserDto>()
            // Skip sensitive or unmapped fields
            .ForProperty(dest => dest.PasswordHash, opt => opt.Skip())
            .ForProperty(dest => dest.SecurityStamp, opt => opt.Skip())

            // Assign a static constant
            .ForProperty(dest => dest.TenantId, opt => opt.UseValue("TENANT-001"))
            .ForProperty(dest => dest.IsActive, opt => opt.UseValue(true));
    }
}
```

### In-Place Existing Object Preservation
When updating an existing destination object (`engine.Map(source, existingDestination)`), skipped properties retain their existing destination value without being reset or overwritten with null/defaults:

```csharp
var existingUser = new UserEntity { PasswordHash = "ExistingSecureHash" };
var updateModel = new UserUpdateModel { FullName = "Jane Doe" };

// existingUser.PasswordHash is preserved!
engine.Map(updateModel, existingUser);
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `opt.Ignore()` | `opt.Skip()` | Replaces `Ignore()` with cleaner semantics |
| `opt.UseValue("constant")` | `opt.UseValue("constant")` | Direct drop-in replacement |
| `opt.MapFrom(src => "constant")` | `opt.UseValue("constant")` | Avoids lambda compilation overhead for static constants |

## Key Benefits
- **Validation Safe:** Configuration validators recognize `.Skip()` as an intentional omission, satisfying `AssertConfigurationIsValid()`.
- **Zero Overhead:** Skipped properties are omitted completely from compiled assignment pipelines.
