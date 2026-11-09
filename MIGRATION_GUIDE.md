# AutoMapper to AutoMapSharp Migration Guide

This guide provides everything you need to migrate an existing .NET application from **AutoMapper** to **AutoMapSharp**.

AutoMapSharp is an independent, 100% cleanroom open-source object-to-object mapping library under the **permissive MIT license**. It offers familiar mapping workflows and LINQ projections. It is in preview, so check [KNOWN_ISSUES.md](KNOWN_ISSUES.md) before migrating production code.

---

## Quick Reference: Syntax Translation Cheat Sheet

| Feature | AutoMapper | AutoMapSharp |
|---|---|---|
| **Namespace** | `using AutoMapper;` | `using AutoMapSharp;` |
| **Projections Namespace** | `using AutoMapper.QueryableExtensions;` | `using AutoMapSharp;` |
| **Module / Profile** | `public class MyProfile : Profile` | `public class MyModule : MappingModule` |
| **Mapping Registration** | `CreateMap<Order, OrderDto>()` | `DefineMap<Order, OrderDto>()` |
| **Property Customization** | `.ForMember(d => d.X, opt => ...)` | `.ForProperty(d => d.X, opt => ...)` |
| **Source Expression** | `opt.MapFrom(s => s.Y)` | `opt.MapSource(s => s.Y)` |
| **Ignore Property** | `opt.Ignore()` | `opt.Skip()` |
| **Constant Value** | `opt.MapFrom(s => "literal")` | `opt.UseValue("literal")` |
| **Conditional Mapping** | `opt.Condition(s => s.Active)` | `opt.If(s => s.Active)` |
| **Null Fallback** | `opt.NullSubstitute("N/A")` | `opt.Fallback("N/A")` |
| **Bidirectional Mapping** | `.ReverseMap()` | `.ReverseMap()` |
| **Type Converters** | `ITypeConverter<TSource, TDest>` | `ITypeConverter<TSource, TDest>` |
| **Type Converter Config** | `.ConvertUsing<MyConverter>()` | `.ConvertUsing<MyConverter>()` |
| **Custom Resolvers** | `IValueResolver<TSource, TDest, TMember>` | `IPropertyResolver<TSource, TDest, TMember>` |
| **Custom Resolver Config**| `.MapFrom<MyResolver>()` | `.MapWith<MyResolver>()` |
| **Lifecycle Hooks** | `.BeforeMap((s, d) => ...)` | `.BeforeMap((s, d) => ...)` |
| **Validation** | `mapper.ConfigurationProvider.AssertConfigurationIsValid()` | `engine.AssertConfigurationIsValid()` |
| **DI Registration** | `services.AddAutoMapper(typeof(Program));` | `services.AddAutoMapSharp(typeof(Program));` |
| **Engine Interface** | `IMapper` | `IMapEngine` |
| **LINQ Projections** | `queryable.ProjectTo<OrderDto>(config)` | `queryable.ProjectOnto<OrderDto>(engine)` |
| **Explicit Expansion** | `opt.ExplicitExpansion()` | `opt.ExplicitExpansion()` |
| **Inline Configuration** | `new MapperConfiguration(cfg => ...)` | `new MappingConfiguration(cfg => ...)` |
| **Pre-Condition** | `opt.PreCondition(s => ...)` | `opt.PreCondition(s => ...)` |
| **Condition with values** | `opt.Condition((s, d, srcMember) => ...)` | `opt.If((s, d, value) => ...)` |
| **Nested Destination** | `.ForPath(d => d.A.B, opt => ...)` | `.ForPath(d => d.A.B, opt => ...)` |
| **Constructor Parameter** | `.ForCtorParam("name", opt => ...)` | `.ForCtorParam("name", opt => ...)` |
| **Value Converter** | `opt.ConvertUsing<MyValueConverter, long>(s => s.X)` | `opt.ConvertUsing<MyValueConverter, long>(s => s.X)` |
| **Member Transform** | `opt.AddTransform(v => ...)` | `opt.AddTransform(v => ...)` |
| **Keep Destination Object** | `opt.UseDestinationValue()` | `opt.UseDestinationValue()` |
| **Mapping Action** | `.AfterMap<MyAction>()` | `.AfterMap<MyAction>()` |
| **Inheritance** | `.IncludeAllDerived()`, `.As<T>()` | `.IncludeAllDerived()`, `.As<T>()` |
| **Open Generics** | `CreateMap(typeof(A<>), typeof(B<>))` | `DefineMap(typeof(A<>), typeof(B<>))` |
| **Per-Call Items** | `mapper.Map<D>(s, o => o.Items["k"] = v)` | `engine.Map<D>(s, o => o.Items["k"] = v)` |
| **Validation Member List** | `CreateMap<S, D>(MemberList.Source)` | `DefineMap<S, D>().ValidateMembers(MemberList.Source)` |
| **Attribute Maps** | `[AutoMap(typeof(S))]`, `[Ignore]` | `[MapFrom(typeof(S))]`, `[SkipMap]` |
| **Extension Methods** | `cfg.IncludeSourceExtensionMethods(typeof(X))` | `IncludeSourceExtensionMethods(typeof(X))` |
| **Collection Merging** | `AutoMapper.Collection`, `.EqualityComparison(...)` | `AutoMapSharp.Collections`, `.EqualityComparison(...)` |
| **Execution Plan** | `config.BuildExecutionPlan(...)` | `engine.BuildExecutionPlan(...)` |

For the full list, including features that are not supported yet, see the [AutoMapper parity matrix](docs/automapper-parity-matrix.md).

---

## Automated Migration (Recommended)

AutoMapSharp provides automated tooling to migrate entire codebases with a single command.

### Option A: Using the Standalone CLI Migrator

1. Run the migration CLI on your solution or source directory:
   ```bash
   dotnet run --project src/AutoMapSharp.Cli -- ./src
   ```
   Or install as a global tool:
   ```bash
   dotnet tool install -g dotnet-automapsharp-migrate
   automapsharp-migrate ./src
   ```

2. To preview changes before writing them to disk:
   ```bash
   automapsharp-migrate ./src --dry-run
   ```

### Option B: Using the Roslyn Migration Analyzer

1. Add `AutoMapSharp.MigrationAnalyzer` to your project:
   ```bash
   dotnet add package AutoMapSharp.MigrationAnalyzer
   ```

2. Run `dotnet format`:
   ```bash
   dotnet format analyzers --diagnostics=AMSMIG001,AMSMIG002,AMSMIG003,AMSMIG004,AMSMIG005,AMSMIG006,AMSMIG007,AMSMIG008
   ```

### What the automated tools change

* The analyzer only reports AutoMapper APIs. When the AutoMapper package is still referenced, it checks each symbol's namespace. When the package has already been removed, it only reports files that import an `AutoMapper` namespace.
* The CLI migrator works on source text without compiling it, so it skips files that don't import AutoMapper. In a file that uses both AutoMapper and another API with the same method names, such as EF Core's `Ignore()`, review the rewritten calls by hand.

---

## Step-by-Step Manual Migration

If you prefer to migrate step-by-step, follow these phases:

### Step 1: Update NuGet Package References

Remove AutoMapper and add AutoMapSharp:

```xml
<!-- Remove -->
<!-- <PackageReference Include="AutoMapper" Version="12.0.1" /> -->
<!-- <PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" /> -->

<!-- Add -->
<PackageReference Include="AutoMapSharp" Version="1.0.0-preview.1" />
<PackageReference Include="AutoMapSharp.Extensions.DependencyInjection" Version="1.0.0-preview.1" />
```

### Step 2: Update Dependency Injection Setup

In `Program.cs` or `Startup.cs`:

```csharp
// Before:
// builder.Services.AddAutoMapper(typeof(Program).Assembly);

// After:
builder.Services.AddAutoMapSharp(typeof(Program).Assembly);
```

Inject `IMapEngine` instead of `IMapper`:

```csharp
// Before:
// public class OrderService(IMapper mapper) { ... }

// After:
public class OrderService(IMapEngine mapper)
{
    public OrderDto GetOrder(Order order) => mapper.Map<Order, OrderDto>(order);
}
```

### Step 3: Update Mapping Profiles to Mapping Modules

Rename base class `Profile` to `MappingModule` and update configuration methods:

```csharp
// Before:
using AutoMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.Customer.FullName))
            .ForMember(d => d.InternalNotes, opt => opt.Ignore())
            .ReverseMap();
    }
}

// After:
using AutoMapSharp;

public class OrderModule : MappingModule
{
    public OrderModule()
    {
        DefineMap<Order, OrderDto>()
            .ForProperty(d => d.CustomerName, opt => opt.MapSource(s => s.Customer.FullName))
            .ForProperty(d => d.InternalNotes, opt => opt.Skip())
            .ReverseMap();
    }
}
```

### Step 4: Update LINQ Expression Projections

Replace `ProjectTo` with `ProjectOnto`:

```csharp
// Before:
// using AutoMapper.QueryableExtensions;
// var dtos = await dbContext.Orders.ProjectTo<OrderSummaryDto>(mapper.ConfigurationProvider).ToListAsync();

// After:
using AutoMapSharp.Projections;
var dtos = await dbContext.Orders.ProjectOnto<OrderSummaryDto>(mapper).ToListAsync();
```

---

## Interactive Migration Workbench

You can test and preview code conversions in real time using the interactive **Migration Playground** included in the sample web application:

```bash
dotnet run --project samples/AutoMapSharp.Sample.Web
```

Navigate to: `http://localhost:5000/Migration/Playground` to paste your existing AutoMapper code and see the instant side-by-side AutoMapSharp conversion!
