# AutoMapSharp.MigrationAnalyzer

This package helps you move a project from AutoMapper to [AutoMapSharp](https://www.nuget.org/packages/AutoMapSharp). Add it to a project that still uses AutoMapper. Your IDE then highlights each AutoMapper call and offers a quick fix that rewrites it.

## Install

```bash
dotnet add package AutoMapSharp.MigrationAnalyzer
```

Build the project, or open it in Visual Studio, Rider or VS Code. Put the cursor on a highlighted line and press **Ctrl+.** (or **Alt+Enter** in Rider) to apply the fix. You can fix one call, a whole file or the whole solution at once.

## What it rewrites

| Rule | AutoMapper | AutoMapSharp |
|---|---|---|
| AMSMIG001 | `using AutoMapper;` | `using AutoMapSharp;` |
| AMSMIG002 | `: Profile` | `: MappingModule` |
| AMSMIG003 | `CreateMap<S, D>()` | `DefineMap<S, D>()` |
| AMSMIG004 | `.ForMember(...)` | `.ForProperty(...)` |
| AMSMIG005 | `.MapFrom(...)` | `.MapSource(...)` |
| AMSMIG006 | `.Ignore()` | `.Skip()` |
| AMSMIG007 | `.ProjectTo<T>()` | `.ProjectOnto<T>()` |
| AMSMIG008 | `IMapper` | `IMapEngine` |

## A suggested path

1. Add this package and `AutoMapSharp` to your project.
2. Apply the fixes across the solution, then remove the AutoMapper packages.
3. Build, and use the [migration guide](https://github.com/mrehman29/AutoMapSharp/blob/main/MIGRATION_GUIDE.md) for anything left over, such as resolvers and `NullSubstitute`.
4. Call `AssertConfigurationIsValid()` in a test so any map that changed meaning shows up straight away.
5. Remove this package once the migration is done.

Prefer the command line? The [dotnet-automapsharp-migrate](https://www.nuget.org/packages/dotnet-automapsharp-migrate) tool applies the same rules to a whole folder. Setup details are in the [analyzer setup guide](https://github.com/mrehman29/AutoMapSharp/blob/main/docs/migration/analyzer-setup.md).
