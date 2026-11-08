# Roslyn Analyzers and Compile-Time Diagnostics

AutoMapSharp ships with first-party Roslyn diagnostic analyzers that inspect your mapping modules at compile time. Instead of waiting for runtime mapping exceptions or subtle null reference bugs in production, AutoMapSharp flags mapping mistakes directly in your IDE (Visual Studio, Visual Studio Code, Rider) and build pipeline.

---

## Included Diagnostic Rules

| Diagnostic ID | Severity | Category | Description |
|---|---|---|---|
| **`AMS001`** | **Warning** | Usage | Destination property has no mapping source and is not configured with `.Skip()`. |
| **`AMS002`** | **Error** | Usage | Source and destination property types are incompatible with no registered converter. |
| **`AMS003`** | **Warning** | Design | Recursive or circular object graph detected without `.MaxDepth()` or `.PreserveReferences()`. |

---

## Detailed Diagnostic Explanations

### `AMS001`: Unmapped Destination Property

#### The Problem
When a destination type introduces a new property that has no matching source property (and cannot be flattened), it defaults to `null` or `0`. In enterprise domain models, unpopulated fields often lead to downstream runtime failures.

```csharp
public class User
{
    public string Name { get; set; } = string.Empty;
}

public class UserDto
{
    public string Name { get; set; } = string.Empty;
    public string SecurityToken { get; set; } = string.Empty; // Unmapped!
}

public class UserMappingModule : MappingModule
{
    public UserMappingModule()
    {
        // Triggers AMS001: Destination property 'SecurityToken' is not mapped
        DefineMap<User, UserDto>();
    }
}
```

#### How to Resolve
You have two options:
1. **Explicitly ignore the property** if it should not be populated:
   ```csharp
   DefineMap<User, UserDto>()
       .ForProperty(dest => dest.SecurityToken, opt => opt.Skip());
   ```
2. **Explicitly map from another source property or value**:
   ```csharp
   DefineMap<User, UserDto>()
       .ForProperty(dest => dest.SecurityToken, opt => opt.MapSource(src => GenerateToken(src)));
   ```

---

### `AMS002`: Incompatible Property Types

#### The Problem
A destination property shares a name with a source property, but the types cannot be automatically or implicitly converted (e.g. attempting to map `DateTime` directly into an `int`, or a complex entity into an incompatible primitive).

```csharp
public class AuditRecord
{
    public DateTime CreatedAt { get; set; }
}

public class AuditRecordDto
{
    public int CreatedAt { get; set; } // Incompatible type: DateTime to int
}

public class AuditMappingModule : MappingModule
{
    public AuditMappingModule()
    {
        // Triggers AMS002: Property 'CreatedAt' type 'DateTime' is incompatible with 'int'
        DefineMap<AuditRecord, AuditRecordDto>();
    }
}
```

#### How to Resolve
Provide an explicit converter, value transformer, or property mapping expression:
```csharp
DefineMap<AuditRecord, AuditRecordDto>()
    .ForProperty(dest => dest.CreatedAt, opt => opt.MapSource(src => (int)src.CreatedAt.Ticks));
```

---

### `AMS003`: Potential Circular Reference

#### The Problem
An entity references itself or has a cyclic reference back to a parent (e.g., `Employee.Manager -> Employee` or `Order -> Customer -> Orders`). Without explicit cycle controls, recursive mapping algorithms will trigger a `StackOverflowException`.

```csharp
public class Employee
{
    public string Name { get; set; } = string.Empty;
    public Employee? Manager { get; set; } // Recursive reference
}

public class EmployeeDto
{
    public string Name { get; set; } = string.Empty;
    public EmployeeDto? Manager { get; set; }
}

public class EmployeeMappingModule : MappingModule
{
    public EmployeeMappingModule()
    {
        // Triggers AMS003: Type 'Employee' contains a recursive reference without MaxDepth or PreserveReferences
        DefineMap<Employee, EmployeeDto>();
    }
}
```

#### How to Resolve
Specify `.MaxDepth(...)` or `.PreserveReferences()` on the mapping definition:
```csharp
DefineMap<Employee, EmployeeDto>()
    .MaxDepth(3)
    .PreserveReferences();
```

---

## How AMS001 matches members

AMS001 uses the same name matching as the runtime engine. The matching code is one source file compiled into both, so the analyzer reports a member exactly when the engine would leave it unmapped:

1. The same name, ignoring case. Public properties and public fields count.
2. The same words after the module's prefixes and suffixes, using its naming convention. The analyzer reads `RecognizePrefixes`, `RecognizeSuffixes`, `ClearPrefixes` and a snake_case `SourceNamingConvention` from the module class. It can only read string literal arguments.
3. A parameterless method named with a recognized prefix, such as `GetTotal()` for `Total`.
4. Flattening, such as `CustomerName` from `Customer.Name`.

Earlier versions also stripped endings like `Dto`, `Model` and `Entity`, which the engine never did, so the two could disagree. That was finding F27.

AMS002 accepts every conversion the engine supports, including enum to enum and user-defined structs.

## Enforcing in Build Pipelines

To turn AutoMapSharp analyzer warnings into compilation errors in continuous integration (CI):

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningsAsErrors>$(WarningsAsErrors);AMS001;AMS003</WarningsAsErrors>
</PropertyGroup>
```

When enabled, any unmapped property or unhandled recursion will halt the build immediately with actionable error messages.
