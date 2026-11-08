# Source Extension Methods

Extension methods on the source type can act as source members.

```csharp
public static class MemberExtensions
{
    public static string GetFullName(this Member member) => member.First + " " + member.Last;
    public static int VisitCount(this Member member) => member.Visits.Count;
}

public class MemberModule : MappingModule
{
    public MemberModule()
    {
        IncludeSourceExtensionMethods(typeof(MemberExtensions));
        DefineMap<Member, MemberBadge>(); // FullName <- GetFullName(), VisitCount <- VisitCount()
    }
}
```

* The type must be a static class. Only methods with one parameter, that return a value and whose parameter type accepts the source type, are used.
* A method matches a destination member by name, or by name with a recognized prefix such as `Get`.
* Properties, fields and naming-convention matches are tried first. Extension methods come before flattening.
* The setting applies to every map in the module.
* The analyzer reads `IncludeSourceExtensionMethods(typeof(...))`, so these members are not reported as unmapped.
* In projections, the method call is passed to the LINQ provider, which usually cannot translate a custom method.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `cfg.IncludeSourceExtensionMethods(typeof(MemberExtensions))` | `IncludeSourceExtensionMethods(typeof(MemberExtensions))` in the module |
