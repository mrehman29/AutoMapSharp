# Core Engine Interface (`IMapEngine`)

## Overview
`IMapEngine` is the primary entry point for executing object mappings in `AutoMapSharp`. It replaces AutoMapper's `IMapper` interface while providing full support for new instance instantiation, in-place updates of existing destination instances (critical for ORM entity tracking), untyped dynamic mappings, and runtime reflection mappings.

## AutoMapSharp Usage

### 1. Mapping to a New Instance
```csharp
var customerDto = engine.Map<Customer, CustomerDto>(customer);
```

### 2. Updating an Existing Destination Instance (e.g. EF Core Entity)
```csharp
// Updates properties directly on the existing instance without breaking change tracking
engine.Map<UpdateCustomerDto, Customer>(updateDto, trackedEntity);
await dbContext.SaveChangesAsync();
```

### 3. Untyped / Runtime Mapping
```csharp
object dynamicSource = GetSource();
var dto = engine.Map<CustomerDto>(dynamicSource);
```

### 4. Dynamic Type Reflection Mapping
```csharp
object result = engine.Map(sourceObject, typeof(Customer), typeof(CustomerDto));
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Notes |
|---|---|---|
| `IMapper` | `IMapEngine` | Renamed interface |
| `mapper.Map<TDest>(src)` | `engine.Map<TDest>(src)` | Direct equivalent |
| `mapper.Map<TSrc, TDest>(src)` | `engine.Map<TSrc, TDest>(src)` | Direct equivalent |
| `mapper.Map(src, dest)` | `engine.Map(src, dest)` | In-place update |
| `mapper.Map(src, srcType, destType)` | `engine.Map(src, srcType, destType)` | Dynamic reflection mapping |

## How the engine runs a map

* **Configuration is frozen when the engine is built.** Module settings are applied to every map in the module, `IncludeBase` rules are merged in, and two modules that define the same type pair cause an `AutoMapSharpConfigurationException`. `FindDescriptor` returns this frozen configuration, and it cannot be changed.
* **Each type pair is compiled once.** The first map of a type pair builds one expression tree for it and compiles it. Later maps of that pair only run the compiled code. Nested objects call the compiled plan of their own type pair directly.
* **Decisions are made when the plan is built.** Which source member feeds each destination member, which conversion applies and which constructor to call are all decided once. No conversion is tried and caught at run time.
* **Errors name the member.** An exception thrown while mapping is wrapped in an `AutoMapSharpMappingException` whose message names the member, such as `Error mapping member 'Total' from 'Order' to 'OrderDto'`. The original exception is the inner exception.
* **The engine is thread-safe.** Create it once, for example as a singleton, and share it.

### Performance

From the BenchmarkDotNet run on 2026-11-07, .NET 8 on Apple M3 Ultra:

| Scenario | AutoMapSharp | AutoMapper 16.3.0 |
|---|---|---|
| Flat object, 10 properties | 30.2 ns | 46.1 ns |
| Nested order with 3 lines | 154.2 ns | 112.2 ns |
| List of 1,000 objects | 25.3 µs | 19.6 µs |

See the [performance report](../benchmarks/performance-report.md) for all scenarios.

### Native AOT

Maps emitted by the [source generator](source-generator.md) run without expression trees and work under Native AOT, which CI verifies with a native binary. `GetPlanInfo` and `DescribePlans` show which maps use generated code. See [Native AOT & Trimming](native-aot-and-trimming.md).
