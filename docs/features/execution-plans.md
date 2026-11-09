# Execution Plans & `CompileMappings`

## Seeing what the engine runs

```csharp
LambdaExpression plan = engine.BuildExecutionPlan(typeof(Member), typeof(MemberBadge));
Console.WriteLine(plan);
```

The plan is the expression tree the engine compiles for that type pair. It shows how the destination is created, which source member feeds each destination member, the conversions, and the callbacks. Polymorphic dispatch, type converters, `PreserveReferences` and `MaxDepth` run around the plan and are not part of it.

In a debugger, the expression's `DebugView` property gives the most readable output. The sample web app's **Execution Plans** page shows it.

## Compiling at start-up

Plans are compiled on first use. To pay that cost at start-up instead, for example before a service takes traffic:

```csharp
var engine = new MapEngine(modules);
engine.CompileMappings();
```

`MappingConfiguration` has the same two methods.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `config.BuildExecutionPlan(typeof(S), typeof(D))` | `engine.BuildExecutionPlan(typeof(S), typeof(D))` |
| `config.CompileMappings()` | `engine.CompileMappings()` |
