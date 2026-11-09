# Installing and Configuring the AutoMapSharp Migration Analyzer

When migrating an existing .NET codebase from AutoMapper to AutoMapSharp, manual find-and-replace can be error-prone and time-consuming. AutoMapSharp provides the `AutoMapSharp.MigrationAnalyzer` package to automate the detection and remediation of legacy AutoMapper APIs.

---

## Installation

Add the analyzer package to your project or `Directory.Build.props`:

```bash
dotnet add package AutoMapSharp.MigrationAnalyzer
```

Or via `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="AutoMapSharp.MigrationAnalyzer" Version="1.0.0-*" PrivateAssets="all" />
</ItemGroup>
```

---

## Migration Diagnostic Rules (`AMSMIG*`)

The migration analyzer flags the following 8 legacy AutoMapper constructs:

| Rule ID | Legacy AutoMapper Syntax | Recommended AutoMapSharp Replacement |
|---|---|---|
| **`AMSMIG001`** | `using AutoMapper;` | `using AutoMapSharp;` |
| **`AMSMIG002`** | `class ... : Profile` | `class ... : MappingModule` |
| **`AMSMIG003`** | `CreateMap<T, U>()` | `DefineMap<T, U>()` |
| **`AMSMIG004`** | `.ForMember(...)` | `.ForProperty(...)` |
| **`AMSMIG005`** | `.MapFrom(...)` | `.MapSource(...)` |
| **`AMSMIG006`** | `.Ignore()` | `.Skip()` |
| **`AMSMIG007`** | `.ProjectTo<T>()` | `.ProjectOnto<T>()` |
| **`AMSMIG008`** | `IMapper` | `IMapEngine` |

---

## Executing Automated Fixes

Once the analyzer is installed, you can apply code fixes in two ways:

### 1. In Your IDE (Visual Studio / Rider / VS Code)
Hover over any highlighted AutoMapper usage, click the lightbulb `💡`, and choose:
> **"Migrate AutoMapper syntax to AutoMapSharp"** $\rightarrow$ **"Fix all in Solution"**

### 2. Using `dotnet format`
Run from your terminal in the solution root:

```bash
dotnet format analyzers --diagnostics=AMSMIG001,AMSMIG002,AMSMIG003,AMSMIG004,AMSMIG005,AMSMIG006,AMSMIG007,AMSMIG008
```

### 3. Using the Standalone CLI Tool
Install and run the dedicated global tool:

```bash
dotnet tool install -g dotnet-automapsharp-migrate
automapsharp-migrate ./src
```
