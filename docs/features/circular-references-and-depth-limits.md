# Circular References & Recursion Protection (`MaxDepth`, `PreserveReferences`)

## Overview
Domain graphs often feature bi-directional links (e.g. `Parent.Children[0].Parent`) or self-referencing relationships (e.g. `Employee.Manager -> Employee`). Naive mapping engines encounter recursive loops leading to fatal `StackOverflowException`s.

AutoMapSharp solves this with two complementary mechanisms:
1. **`PreserveReferences(true)`:** Tracks already-mapped objects by reference identity and returns existing mapped instances when revisited, faithfully reproducing circular graphs.
2. **`MaxDepth(depth)`:** Enforces a hard ceiling on recursion depth, returning `null`/default when the limit is reached.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class Employee
{
    public string Name { get; set; } = string.Empty;
    public Employee? Manager { get; set; }
    public List<Employee> DirectReports { get; set; } = new();
}

public class EmployeeDto
{
    public string Name { get; set; } = string.Empty;
    public EmployeeDto? Manager { get; set; }
    public List<EmployeeDto> DirectReports { get; set; } = new();
}

public class OrgChartModule : MappingModule
{
    public OrgChartModule()
    {
        DefineMap<Employee, EmployeeDto>()
            .PreserveReferences(true)
            .MaxDepth(3);
    }
}
```

```csharp
var manager = new Employee { Name = "Alice" };
var employee = new Employee { Name = "Bob", Manager = manager };
manager.DirectReports.Add(employee);

var managerDto = engine.Map<Employee, EmployeeDto>(manager);

// Circular reference preserved with exact reference identity:
Assert.Same(managerDto, managerDto.DirectReports[0].Manager);
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `CreateMap<S, D>().PreserveReferences()` | `DefineMap<S, D>().PreserveReferences(true)` | Direct drop-in replacement |
| `CreateMap<S, D>().MaxDepth(3)` | `DefineMap<S, D>().MaxDepth(3)` | Hard recursion guard |

## How It Works Under the Hood
- `PreserveReferences` remembers one destination object per source object **and destination type**, by reference identity, so custom `Equals` overrides on source objects have no effect. The same source object can be mapped to a `PersonDto` and a `PersonCard` in one graph, and each destination type keeps its own shared instance.
- The new destination object is registered before its members are mapped, so a child that points back to its parent gets the same parent instance.
- `MaxDepth` increments a per-type counter on `ResolutionContext` during recursive calls and decrements in a `finally` block, ensuring zero resource leakage.
