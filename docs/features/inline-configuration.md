# Inline Configuration (`MappingConfiguration`)

Maps can be defined inline, without writing a module class. This is AutoMapper's `MapperConfiguration` pattern.

## Usage

```csharp
var config = new MappingConfiguration(cfg =>
{
    cfg.SourceNamingConvention = SnakeCaseNamingConvention.Instance;
    cfg.ValueTransformers.Add<string>(s => s?.Trim()!);

    cfg.DefineMap<Order, OrderDto>()
        .ForProperty(d => d.Total, o => o.MapSource(s => s.Lines.Sum(l => l.Price)));

    cfg.AddModule<CustomerModule>();
    cfg.AddModule(new LegacyModule("eu"));
    cfg.AddModules(typeof(CustomerModule).Assembly);
});

config.AssertConfigurationIsValid();
IMapEngine engine = config.CreateEngine();
```

For a one-off engine, pass the configuration straight to `MapEngine`:

```csharp
var engine = new MapEngine(cfg => cfg.DefineMap<Order, OrderDto>());
```

With dependency injection:

```csharp
services.AddAutoMapSharp(options => options.AddMaps(cfg => cfg.DefineMap<Order, OrderDto>()));
```

## Notes

* The builder offers the same module-level settings as `MappingModule`: naming conventions, prefixes, suffixes, value transformers and the null collection policy. They apply to the inline maps only. Added modules keep their own settings.
* Engines created from one `MappingConfiguration` share compiled plans. `CreateEngine(serviceProvider)` gives an engine that creates resolvers from that provider.
* `AddModules(assembly)` adds public modules with a parameterless constructor.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `new MapperConfiguration(cfg => cfg.CreateMap<A, B>())` | `new MappingConfiguration(cfg => cfg.DefineMap<A, B>())` |
| `cfg.AddProfile<MyProfile>()` | `cfg.AddModule<MyModule>()` |
| `cfg.AddMaps(assembly)` | `cfg.AddModules(assembly)` |
| `config.CreateMapper()` | `config.CreateEngine()` |
| `config.AssertConfigurationIsValid()` | `config.AssertConfigurationIsValid()` |
