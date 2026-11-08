# AutoMapper Parity Matrix

This is the single list of AutoMapper features and their status in AutoMapSharp. The sample web app's **Parity Matrix** page reads this file.

Status values:

* **Supported**: works and is covered by tests.
* **Partial**: works with a limitation described in the notes.
* **Planned**: on the roadmap but not built yet.
* **Not planned**: not on the roadmap.

| Area | AutoMapper | AutoMapSharp | Status | Notes |
|---|---|---|---|---|
| Configuration | `Profile` | `MappingModule` | Supported | [Mapping modules](features/mapping-modules.md) |
| Configuration | `new MapperConfiguration(cfg => ...)` | `new MappingConfiguration(cfg => ...)`, `new MapEngine(cfg => ...)` | Supported | [Inline configuration](features/inline-configuration.md) |
| Configuration | `CreateMap<S, D>()` | `DefineMap<S, D>()` | Supported | [Fluent configuration](features/fluent-configuration-overview.md) |
| Configuration | `CreateMap(typeof(S), typeof(D))` | `DefineMap(typeof(S), typeof(D))` | Supported | Members are configured by name. [Open generics](features/open-generic-maps.md) |
| Configuration | Open generic maps | `DefineMap(typeof(Page<>), typeof(PageDto<>))` | Supported | Converters can be open generic too. [Open generics](features/open-generic-maps.md) |
| Configuration | `ReverseMap()` with unflattening | `ReverseMap()` with unflattening | Supported | [ForPath & unflattening](features/forpath-and-unflattening.md) |
| Configuration | Duplicate map detection | Engine constructor throws | Supported | Names both modules |
| Configuration | `ShouldMapProperty`, `ShouldMapField`, `ShouldMapMethod` | Same names on `MappingModule` | Supported | [Property matching](features/property-matching-conventions.md) |
| Configuration | Public field mapping | Public fields on both sides | Supported | [Property matching](features/property-matching-conventions.md) |
| Configuration | `IncludeMembers` | None | Planned | Not built yet |
| Configuration | `ForAllMaps`, `ForAllPropertyMaps` | None | Planned | Not built yet |
| Configuration | Attribute maps: `[AutoMap]`, `[Ignore]`, `[SourceMember]` | `[MapFrom]`, `[SkipMap]`, `[SourceMember]` | Supported | [Attribute configuration](features/attribute-mapping.md) |
| Configuration | `IncludeSourceExtensionMethods` | `IncludeSourceExtensionMethods` | Supported | [Source extension methods](features/source-extension-methods.md) |
| Conventions | Source and destination naming conventions | `SourceNamingConvention`, `DestinationNamingConvention` | Supported | PascalCase, camelCase, snake_case. [Naming conventions](features/naming-conventions.md) |
| Conventions | `RecognizePrefixes`, `RecognizePostfixes` | `RecognizePrefixes`, `RecognizeSuffixes` | Supported | [Naming conventions](features/naming-conventions.md) |
| Conventions | `RecognizeDestinationPrefixes`, `RecognizeDestinationPostfixes` | `RecognizeDestinationPrefixes`, `RecognizeDestinationSuffixes` | Supported | [Naming conventions](features/naming-conventions.md) |
| Conventions | `ClearPrefixes` | `ClearPrefixes` | Supported | Also removes the default `Get` |
| Conventions | `ReplaceMemberName` | `ReplaceMemberName` | Supported | Applied to source member names |
| Conventions | `RecognizeAlias` | None | Not planned | Use `MapSource` |
| Conventions | Flattening and `GetX()` methods | Same | Supported | [Object flattening](features/object-flattening.md) |
| Members | `ForMember(d => d.X, o => o.MapFrom(s => ...))` | `ForProperty(d => d.X, o => o.MapSource(s => ...))` | Supported | Null-safe member chains |
| Members | `MapFrom<TResolver>()` with `IValueResolver` | `MapWith<TResolver>()` with `IPropertyResolver` | Supported | `Resolve` has no destination member parameter. [Resolvers](features/custom-property-resolvers.md) |
| Members | `IMemberValueResolver` | None | Partial | Use `IPropertyResolver`, which receives the whole source, or `IValueConverter` |
| Members | `Ignore()` | `Skip()` | Supported | |
| Members | `Condition(...)` | `If(...)` | Supported | Overloads receive the new value and the current value. [Conditions](features/conditional-mapping-and-fallbacks.md) |
| Members | `PreCondition(...)` | `PreCondition(...)` | Supported | Skips the resolver too |
| Members | `NullSubstitute(...)` | `Fallback(...)` | Supported | |
| Members | `UseValue(...)` | `UseValue(...)` | Supported | |
| Members | `ConvertUsing(IValueConverter)` | `ConvertUsing<TValueConverter>()` | Supported | [Value converters](features/value-converters.md) |
| Members | `AddTransform(...)` | `AddTransform(...)` | Supported | [Value transformers](features/value-transformers.md) |
| Members | `UseDestinationValue()`, `DoNotUseDestinationValue()` | Same | Supported | [Existing collection updates](features/existing-collection-updates.md) |
| Members | `ForPath(...)` | `ForPath(...)` | Supported | [ForPath & unflattening](features/forpath-and-unflattening.md) |
| Members | `ForCtorParam(...)` | `ForCtorParam(...)` | Supported | [Constructors & records](features/constructor-mapping-and-records.md) |
| Members | `ForSourceMember(..., o => o.DoNotValidate())` | Same | Supported | |
| Members | `SetMappingOrder(...)` | None | Planned | Members map in declaration order today |
| Members | `ExplicitExpansion()` | `ExplicitExpansion()` | Supported | Projections only |
| Types | `ConvertUsing(...)` with a function or `ITypeConverter` | Same | Supported | `Convert` takes (source, context) |
| Types | `ConstructUsing(...)`, with or without the context | Same | Supported | |
| Types | `BeforeMap`, `AfterMap` with lambdas | Same | Supported | [Lifecycle callbacks](features/lifecycle-callbacks.md) |
| Types | `BeforeMap<T>()`, `AfterMap<T>()` with `IMappingAction` | Same | Supported | Created from DI |
| Types | `Include`, `IncludeBase` | Same | Supported | Several `IncludeBase` calls are allowed. [Polymorphism](features/polymorphic-subclass-mapping.md) |
| Types | `IncludeAllDerived()` | `IncludeAllDerived()` | Supported | Picks the closest derived map |
| Types | `As<T>()` | `As<T>()` | Supported | |
| Types | `MaxDepth`, `PreserveReferences` | Same | Supported | References are kept per destination type. [Circular references](features/circular-references-and-depth-limits.md) |
| Execution | `Map<D>(src)`, `Map<S, D>(src)`, `Map(src, dest)`, `Map(src, srcType, destType)` | Same | Supported | [Core engine](features/core-engine-interface.md) |
| Execution | `Map(src, opts => opts.Items[...])` | Same | Supported | [Per-call options](features/per-call-options.md) |
| Execution | Per-call `BeforeMap`, `AfterMap` | Same | Supported | Run for the top-level object |
| Execution | Collections, sets, dictionaries, immutable collections | Same | Supported | [Collections](features/collections-mapping.md) |
| Execution | Enum to enum by name | Same | Supported | [Enum mapping](features/enum-mapping.md) |
| Execution | Built-in type conversions | Same | Supported | [Type conversions](features/type-conversions.md) |
| Execution | Mapping from and to `IDictionary<string, object>` and `ExpandoObject` | Same | Supported | [Dictionaries](features/dictionary-mapping.md) |
| Execution | `BuildExecutionPlan`, `CompileMappings` | Same | Supported | [Execution plans](features/execution-plans.md) |
| Projection | `ProjectTo<T>()` | `ProjectOnto<T>()` | Supported | [ORM projection](features/orm-projection-overview.md) |
| Projection | Parameterized projections | Same | Supported | SQL parameters. [Parameterized projections](features/parameterized-projections.md) |
| Projection | Explicit expansion by name | Same | Supported | [Explicit expansion](features/explicit-expansion.md) |
| Projection | Explicit expansion by expression | `ProjectOnto<T>(engine, null, d => d.Items)` | Supported | [Explicit expansion](features/explicit-expansion.md) |
| Projection | `NullSubstitute`, inheritance and `MaxDepth` in projections | `Fallback` as `COALESCE`, type checks, depth limit | Supported | [ORM projection](features/orm-projection-overview.md) |
| Projection | Resolvers, value converters and conditions in projections | Rejected with a clear error | Partial | They run .NET code. AutoMapper also cannot translate most of them |
| Validation | `AssertConfigurationIsValid()` | `AssertConfigurationIsValid()`, `ValidateConfiguration()` | Supported | Also checks types, constructors and nested automatic maps. [Validation](features/configuration-validation.md) |
| Validation | `MemberList.Source`, `MemberList.None` | `ValidateMembers(MemberList.Source)` | Supported | Reverse maps default to `None`, like AutoMapper |
| DI | `AddAutoMapper(typeof(Program))` | `AddAutoMapSharp(typeof(Program))` | Supported | [Dependency injection](features/dependency-injection.md) |
| DI | Transient `IMapper` with scoped services | Transient `IMapEngine` | Supported | |
| DI | Resolver and converter scanning | Same | Supported | |
| Ecosystem | `AutoMapper.Collection` | `AutoMapSharp.Collections` | Supported | [Collection merging](features/collection-merging.md) |
| Ecosystem | `AutoMapper.Extensions.ExpressionMapping` | None | Not planned | |
| Platform | Native AOT | Supported for generated maps | Partial | Maps the source generator emits run as a native binary, verified in CI. [Native AOT](features/native-aot-and-trimming.md) |
| Tooling | None | Source generator for eligible maps | Supported | [Source generator](features/source-generator.md) |
| Tooling | None | Roslyn analyzers AMS001 to AMS003 | Supported | Share name matching with the engine |
| Tooling | None | Migration analyzer and CLI | Supported | [Migration guide](../MIGRATION_GUIDE.md) |
