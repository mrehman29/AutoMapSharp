# dotnet-automapsharp-migrate

A .NET tool that rewrites AutoMapper code to [AutoMapSharp](https://www.nuget.org/packages/AutoMapSharp) across a whole folder. It uses the same rules as the [AutoMapSharp.MigrationAnalyzer](https://www.nuget.org/packages/AutoMapSharp.MigrationAnalyzer) package.

## Install

```bash
dotnet tool install --global dotnet-automapsharp-migrate --prerelease
```

## Usage

Commit your work first, so you can review the changes as a diff. Then preview what the tool would change:

```bash
automapsharp-migrate ./src --dry-run
```

When the preview looks right, run it for real:

```bash
automapsharp-migrate ./src
```

You can pass a folder or a single `.cs` file. With no path, it uses the current folder. It skips `bin`, `obj` and `.git`.

## What it rewrites

- **Namespaces and base classes.** `using AutoMapper;` becomes `using AutoMapSharp;`, and `Profile` becomes `MappingModule`.
- **Map definitions.** `CreateMap` becomes `DefineMap`, `ForMember` becomes `ForProperty`, `MapFrom` becomes `MapSource` and `Ignore` becomes `Skip`.
- **Options.** `Condition` becomes `If`, `NullSubstitute` becomes `Fallback`, and `MapFrom<TResolver>()` becomes `MapWith<TResolver>()`.
- **Resolvers.** `IValueResolver` becomes `IPropertyResolver`. You still need to remove the `destMember` parameter from each `Resolve` method by hand.
- **Usage.** `ProjectTo<T>(mapper.ConfigurationProvider)` becomes `ProjectOnto<T>(mapper)`, and `IMapper` becomes `IMapEngine`.

Each change is printed with its file, line and rule ID, so you can check it against your diff. The tool only touches files that import an AutoMapper namespace.

After the tool runs, swap the AutoMapper packages for AutoMapSharp, build, and follow the [migration guide](https://github.com/mrehman29/AutoMapSharp/blob/main/MIGRATION_GUIDE.md) for anything it could not convert.
