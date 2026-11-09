# AutoMapSharp.Extensions.DependencyInjection

This package registers [AutoMapSharp](https://www.nuget.org/packages/AutoMapSharp) with `Microsoft.Extensions.DependencyInjection`. It is the AutoMapSharp version of `AddAutoMapper`.

## Install

```bash
dotnet add package AutoMapSharp.Extensions.DependencyInjection
```

## Usage

Point it at your assembly. It finds your mapping modules, property resolvers, type converters, value converters and mapping actions for you:

```csharp
using AutoMapSharp.Extensions.DependencyInjection;

builder.Services.AddAutoMapSharp(typeof(Program));
```

Then inject `IMapEngine` wherever you need it:

```csharp
public class OrderService(IMapEngine mapper)
{
    public OrderDto Get(Order order) => mapper.Map<OrderDto>(order);
}
```

You can also pass assemblies, list module types, or add modules one at a time:

```csharp
builder.Services.AddAutoMapSharp(typeof(OrderModule).Assembly, typeof(CustomerModule).Assembly);

builder.Services.AddAutoMapSharp(options => options.AddModule<OrderModule>());
```

## How it is registered

- **The configuration is a singleton.** Maps are checked and compiled once for the life of the app.
- **`IMapEngine` is transient and cheap.** Resolvers and converters are created from the scope that resolved the engine, so a resolver can take a scoped service such as a `DbContext`.

## Moving from AutoMapper

Replace `services.AddAutoMapper(...)` with `services.AddAutoMapSharp(...)` and `IMapper` with `IMapEngine`. The [migration guide](https://github.com/mrehman29/AutoMapSharp/blob/main/MIGRATION_GUIDE.md) covers everything else, and the [dependency injection guide](https://github.com/mrehman29/AutoMapSharp/blob/main/docs/features/dependency-injection.md) has more examples.
