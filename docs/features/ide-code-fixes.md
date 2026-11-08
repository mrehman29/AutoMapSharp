# IDE Code Fix Providers

AutoMapSharp includes first-party Roslyn Code Fix Providers that offer one-click lightbulb quick fixes in Visual Studio, JetBrains Rider, and Visual Studio Code (`OmniSharp` / C# Dev Kit).

---

## Quick Fix for `AMS001` (Unmapped Destination Property)

When an unmapped destination property is detected by `AMS001`, your IDE displays a lightbulb `💡` or pressing `Ctrl+.` / `Alt+Enter` offers two instant remediation options:

```
💡 Ignore property 'SecurityToken' using .Skip()
💡 Map property 'SecurityToken' using .MapSource()
```

### Option 1: Ignore Property Using `.Skip()`

Selecting this action automatically appends `.ForProperty(dest => dest.SecurityToken, opt => opt.Skip())` to the mapping chain:

#### Before:
```csharp
public class UserModule : MappingModule
{
    public UserModule()
    {
        DefineMap<User, UserDto>();
    }
}
```

#### After (Applied by Code Fix):
```csharp
public class UserModule : MappingModule
{
    public UserModule()
    {
        DefineMap<User, UserDto>()
            .ForProperty(dest => dest.SecurityToken, opt => opt.Skip());
    }
}
```

### Option 2: Map Property Using `.MapSource()`

Selecting this action generates a stub mapping expression allowing you to specify or adjust the source property:

#### After (Applied by Code Fix):
```csharp
public class UserModule : MappingModule
{
    public UserModule()
    {
        DefineMap<User, UserDto>()
            .ForProperty(dest => dest.SecurityToken, opt => opt.MapSource(src => src.SecurityToken));
    }
}
```

---

## Solution-Wide Batch Fixing (`Fix All in Solution`)

When migrating a large project or adding a new required field across dozens of view models, you can click:

> **"Fix all occurrences in Project"** or **"Fix all occurrences in Solution"**

Roslyn will automatically analyze all diagnostic occurrences across your solution and apply the fix simultaneously.

Alternatively, execute from the command line:

```bash
dotnet format analyzers --diagnostics=AMS001
```
