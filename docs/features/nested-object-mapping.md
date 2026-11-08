# Nested Complex Objects & Sub-Mapper Chaining

## Overview
Enterprise domain graphs are frequently deep hierarchies of nested complex types (e.g., Organization $\rightarrow$ Division $\rightarrow$ Department $\rightarrow$ Manager $\rightarrow$ Contact).

AutoMapSharp automatically resolves and chains child type mappings across arbitrary depths, passing execution context recursively while preserving null safety and change-tracking state.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class EnterpriseModule : MappingModule
{
    public EnterpriseModule()
    {
        // Define mappings for each level
        DefineMap<Organization, OrganizationDto>();
        DefineMap<Division, DivisionDto>();
        DefineMap<Department, DepartmentDto>();
        DefineMap<Manager, ManagerDto>();
    }
}
```

```csharp
var enterpriseOrg = new Organization
{
    OrgName = "Acme Corp",
    Division = new Division
    {
        DivName = "Cloud Systems",
        Department = new Department
        {
            DeptName = "Reliability Engineering",
            Manager = new Manager { FullName = "Sarah Connor" }
        }
    }
};

// Maps all 4 levels recursively!
var dto = engine.Map<Organization, OrganizationDto>(enterpriseOrg);

Assert.Equal("Acme Corp", dto.OrgName);
Assert.Equal("Cloud Systems", dto.Division?.DivName);
Assert.Equal("Reliability Engineering", dto.Division?.Department?.DeptName);
Assert.Equal("Sarah Connor", dto.Division?.Department?.Manager?.FullName);
```

## Migrating from AutoMapper

| AutoMapper Behavior | AutoMapSharp Behavior | Migration Notes |
|---|---|---|
| Automatic nested sub-mapping | Automatic nested sub-mapping | Works identically |
| Null short-circuiting | Null short-circuiting | If any intermediate child is null, returns null without throwing |

## In-Place Nested Updates
When updating an existing entity that already has instantiated child objects, AutoMapSharp updates each existing child object in-place, preserving child entity reference identities across the entire hierarchy.
