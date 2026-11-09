# Dependency Injection Setup (`AutoMapSharp.Extensions.DependencyInjection`)

AutoMapSharp integrates with `Microsoft.Extensions.DependencyInjection`. It scans assemblies for modules, property resolvers and type converters, and registers `IMapEngine`.

## 1. Registering

```csharp
using AutoMapSharp.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Scan an assembly.
builder.Services.AddAutoMapSharp(typeof(Program).Assembly);

// Or pass marker types, like AddAutoMapper(typeof(Program)). A MappingModule type is registered as a module.
// Any other type marks its assembly for scanning.
builder.Services.AddAutoMapSharp(typeof(Program));

// Or configure explicitly.
builder.Services.AddAutoMapSharp(options => options
    .AddModule<OrderModule>()
    .AddModule(new LegacyModule("eu"))
    .AddAssembly(typeof(SharedContracts).Assembly));
```

Scanning registers:

* Every non-abstract `MappingModule`, public or internal, as a singleton.
* Every `IPropertyResolver<,,>`, `ITypeConverter<,>`, `IValueConverter<,>` and `IMappingAction<,>` implementation as transient, unless you registered it already.

Private and protected nested classes are skipped, because they are usually local helpers that build their own engine.

Calling `AddAutoMapSharp` without any assembly or type throws an `ArgumentException`. Earlier versions guessed the calling assembly, which is unreliable.

## 2. Lifetimes

| Service | Lifetime | Why |
|---|---|---|
| The compiled configuration | Singleton | Plans are built once per application |
| `IMapEngine` | Transient | Each one is a cheap view of the singleton that creates resolvers and converters from the scope it was resolved from |
| Modules | Singleton | Read once, when the configuration is built |
| Resolvers and converters | Transient | Created for each map that needs them |

Because `IMapEngine` follows the caller's scope, a resolver can depend on a scoped service such as a `DbContext` or the current user:

```csharp
builder.Services.AddScoped<RequestTrace>();

public class RequestIdResolver : IPropertyResolver<AuditEntry, AuditEntryView, Guid>
{
    private readonly RequestTrace _trace;
    public RequestIdResolver(RequestTrace trace) => _trace = trace;
    public Guid Resolve(AuditEntry source, AuditEntryView destination, ResolutionContext context) => _trace.RequestId;
}
```

When an `IMapEngine` is injected into a singleton, it resolves from the root provider, like any other transient service.

## 3. Resolvers that are not registered

When a resolver or converter is not registered, the engine creates it itself. It uses a public parameterless constructor when there is one. Otherwise it uses the public constructor with the most parameters that the container can supply.

## 4. Without a container

`MapEngine.WithServiceProvider(provider)` returns an engine that shares the compiled plans and creates resolvers from `provider`. Use it when you manage scopes yourself.

```csharp
var engine = new MapEngine(new OrderModule());
using var scope = rootProvider.CreateScope();
var scoped = engine.WithServiceProvider(scope.ServiceProvider);
```

## 5. Migrating from AutoMapper

| AutoMapper | AutoMapSharp |
|---|---|
| `services.AddAutoMapper(typeof(Program));` | `services.AddAutoMapSharp(typeof(Program));` |
| `services.AddAutoMapper(cfg => cfg.AddProfile<MyProfile>());` | `services.AddAutoMapSharp(o => o.AddModule<MyModule>());` |
| Injected `IMapper`, transient | Injected `IMapEngine`, transient |
| Resolvers and converters registered by scanning | Same |

## 6. See it running

The sample web app has a **Scoped Services in Resolvers** page under **Reliability & Parity**. Reload it to see a new request ID.
