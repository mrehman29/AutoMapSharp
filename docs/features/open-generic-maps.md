# Open Generic Maps & `DefineMap(Type, Type)`

## Maps between types known at run time

```csharp
DefineMap(typeof(Person), typeof(PersonDto))
    .ForProperty("Nickname", o => o.MapSource("Name"))
    .ForProperty("Secret", o => o.Skip())
    .ForProperty("Source", o => o.UseValue("import"));
```

Members are configured by name, because there is no lambda to refer to them. The non-generic definition also offers `ReverseMap`, `ConvertUsing(Type)`, `Include(Type, Type)`, `IncludeBase(Type, Type)`, `MaxDepth`, `PreserveReferences` and `ValidateMembers`.

## Open generic maps

```csharp
DefineMap(typeof(Page<>), typeof(PageDto<>))
    .ForProperty("Count", o => o.MapSource("Total"));

DefineMap(typeof(Wrapper<>), typeof(Unwrapped<>))
    .ConvertUsing(typeof(UnwrapConverter<>));
```

```csharp
PageDto<OrderDto> page = engine.Map<Page<Order>, PageDto<OrderDto>>(orders);
```

* The map is closed for each concrete pair the first time it is used, and the closed map is cached.
* An open generic converter is closed with the destination's type arguments, or the source's when the counts differ.
* Element types inside the generic types, such as `Order` to `OrderDto` above, use their own maps.
* Both sides must be open generic types.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `CreateMap(typeof(Page<>), typeof(PageDto<>))` | `DefineMap(typeof(Page<>), typeof(PageDto<>))` |
| `.ForMember("Count", o => o.MapFrom("Total"))` | `.ForProperty("Count", o => o.MapSource("Total"))` |
| `.ConvertUsing(typeof(Converter<>))` | `.ConvertUsing(typeof(Converter<>))` |
