# AutoMapSharp

AutoMapSharp is a free, open-source object mapper for .NET, released under the MIT license.

If you have used AutoMapper, you already know how it works. You describe your maps once, and AutoMapSharp takes care of copying data between your entities, DTOs and view models. Most AutoMapper concepts carry over with a new name, and many keep the same one.

> **Coming from AutoMapper?** Start with the [migration guide](https://github.com/mrehman29/AutoMapSharp/blob/main/MIGRATION_GUIDE.md). It has a side-by-side table of every API, step-by-step instructions, and a list of the small behavior differences to watch for. The [AutoMapSharp.MigrationAnalyzer](https://www.nuget.org/packages/AutoMapSharp.MigrationAnalyzer) package and the [migration tool](https://www.nuget.org/packages/dotnet-automapsharp-migrate) can do most of the rewriting for you.

## Install

```bash
dotnet add package AutoMapSharp
```

For ASP.NET Core and other apps that use `Microsoft.Extensions.DependencyInjection`, add the DI package too:

```bash
dotnet add package AutoMapSharp.Extensions.DependencyInjection
```

## A quick example

Group your maps in a `MappingModule`, the AutoMapSharp version of an AutoMapper `Profile`:

```csharp
using AutoMapSharp;

public class UserModule : MappingModule
{
    public UserModule()
    {
        DefineMap<User, UserDto>()
            .ForProperty(d => d.FullName, o => o.MapSource(s => s.FirstName + " " + s.LastName))
            .ForProperty(d => d.PasswordHash, o => o.Skip())
            .ReverseMap();
    }
}
```

Create an engine and map:

```csharp
var engine = new MapEngine(new UserModule());
engine.AssertConfigurationIsValid(); // fails fast if a destination member has no source

UserDto dto = engine.Map<UserDto>(user);
engine.Map(updateDto, existingUser); // update an existing object in place
```

Or register it with dependency injection and inject `IMapEngine`:

```csharp
builder.Services.AddAutoMapSharp(typeof(Program));
```

Project straight to SQL with EF Core, so only the columns you need are loaded:

```csharp
var orders = await db.Orders.ProjectOnto<OrderDto>(engine).ToListAsync();
```

## What you get

- **Familiar mapping.** Conventions, flattening and unflattening, `ReverseMap`, `ForPath`, constructor and record mapping, enums, collections, dictionaries and `ExpandoObject`.
- **Customization when you need it.** Property resolvers, type converters, value converters, conditions, fallbacks, transforms, `BeforeMap` and `AfterMap`, and inheritance with `Include` and `IncludeBase`.
- **Query projection.** `ProjectOnto` turns your maps into expression trees that EF Core translates to SQL, with parameters and explicit expansion.
- **Configuration checks.** `AssertConfigurationIsValid` catches unmapped members, type mismatches and missing constructors at startup instead of in production.
- **Speed.** Each map is compiled once. A built-in source generator also writes plain C# for maps it can reproduce exactly, which speeds up the first call and makes those maps work under Native AOT.
- **Compile-time help.** Analyzers in the package warn about unmapped properties, incompatible types and circular references while you type.

AutoMapSharp targets .NET Standard 2.0, .NET 8 and .NET 9, so it runs on .NET Framework 4.7.2 and later as well as modern .NET.

## Preview status

This is a preview release. The API is close to final, but it may still change before 1.0.0. Every known issue and its fix is listed in [KNOWN_ISSUES.md](https://github.com/mrehman29/AutoMapSharp/blob/main/KNOWN_ISSUES.md). The [AutoMapper parity matrix](https://github.com/mrehman29/AutoMapSharp/blob/main/docs/automapper-parity-matrix.md) shows which AutoMapper features are supported today.

## Learn more

- [Documentation and feature guides](https://github.com/mrehman29/AutoMapSharp/blob/main/README.md#documentation--samples)
- [Migration guide from AutoMapper](https://github.com/mrehman29/AutoMapSharp/blob/main/MIGRATION_GUIDE.md)
- [AutoMapper parity matrix](https://github.com/mrehman29/AutoMapSharp/blob/main/docs/automapper-parity-matrix.md)
- [Benchmarks against AutoMapper](https://github.com/mrehman29/AutoMapSharp/blob/main/docs/benchmarks/performance-report.md)
- [Report an issue or ask a question](https://github.com/mrehman29/AutoMapSharp/issues)

AutoMapSharp is an independent project. It is not affiliated with or endorsed by AutoMapper or its authors.
