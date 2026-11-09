# Native AOT & Trimming

> **Status:** Maps emitted by the [source generator](source-generator.md) run under Native AOT. CI publishes a native binary and runs it on every build. Maps that are not generated need expression trees and are not supported under Native AOT.

## What works

A map runs under Native AOT when the source generator emits it. Check this at build time with the `AMS100` messages, or at run time:

```csharp
if (engine.GetPlanInfo(typeof(Order), typeof(OrderDto)).Kind != MapPlanKind.Generated)
{
    // This map would fail under Native AOT.
}
```

Generated maps need no extra project settings. The generated registration carries trimming annotations, so the AOT compiler keeps the metadata the engine uses to check a generated map.

## What does not work

* Maps the generator does not emit. They need expression trees, which Native AOT cannot compile reliably. Under Native AOT they fail with an `AutoMapSharpConfigurationException` that names the map and the reason it was not generated.
* `ProjectOnto`, `BuildExecutionPlan`, dictionary mapping and open generic maps, which are built at run time.

## Trim warnings

Publishing with `<PublishAot>true</PublishAot>` reports trim warnings from the library's reflection-based fallback. They do not affect generated maps. Annotating the fallback so these warnings only appear when you use it is planned.

## How CI verifies it

`tests/AutoMapSharp.Tests.NativeAot` maps an object with flattening, a nullable value, an enum, a nested object and a nested list. CI publishes it with Native AOT for `linux-x64` and runs the binary. The test fails if any of its maps is not generated code, or if any value is wrong.

```bash
dotnet publish tests/AutoMapSharp.Tests.NativeAot -c Release -f net8.0 -r linux-x64 -o artifacts/aot
./artifacts/aot/AutoMapSharp.Tests.NativeAot
```

## AutoMapper comparison

| Topic | AutoMapper 16.x | AutoMapSharp |
|---|---|---|
| Native AOT | Not supported | Supported for generated maps |
| Trimming | Not supported | Generated maps are annotated. The fallback still warns |
