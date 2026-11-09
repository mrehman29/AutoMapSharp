# AutoMapSharp Sample Web Application (Interactive Visual Showcase)

This is an official, standalone ASP.NET Core MVC Web Application demonstrating all features of **AutoMapSharp** with a modern visual UI. It directly consumes the locally generated NuGet package (`AutoMapSharp` and `AutoMapSharp.Extensions.DependencyInjection`).

---

## What It Shows

Instead of an API-only project, this web application provides **side-by-side visual comparisons** in your browser so you can easily run it and see the mapping behavior with realistic hardcoded sample database data:

| Feature Demo | URL | Description |
|---|---|---|
| **Feature Dashboard** | `/` | Visual overview of all available demos with navigation cards across all 8 phases. |
| **Deep Flattening & Conversions** | `/Orders` | Side-by-side view showing raw source `Order` (hierarchical) vs mapped `OrderSummaryViewModel` (flattened `Customer.Address.City`, getter methods, enum to string, guid to string, and date conversions). |
| **In-Place Entity Update** | `/Orders/Update` | Interactive demo showing how AutoMapSharp mutates existing tracked EF Core entities in-place without re-instantiation, verified with `ReferenceEquals == true` and mapping into `private set` properties. |
| **Null-Safe Flattening** | `/Orders/NullSafe` | Demonstrates safe short-circuiting when intermediate child objects are null (`Customer = null`) without throwing `NullReferenceException`. |
| **Casing Conventions** | `/Conventions/SnakeCase` | Visual split-screen showing external `snake_case` JSON mapped directly into internal `PascalCase` ViewModels. |
| **Prefix & Suffix Stripping** | `/Conventions/PrefixSuffix` | Demonstrates stripping Hungarian prefixes (`m_Title` -> `Title`) and architectural suffixes (`CategoryModel` -> `Category`). |
| **C# 9+ Positional Records** | `/Records` | Demonstrates mapping into immutable C# records with primary constructors (no parameterless constructor). |
| **Dynamic & Untyped Mapping** | `/Dynamic` | Demonstrates untyped object mapping `_mapper.Map<T>(object)` and runtime dynamic reflection mapping `_mapper.Map(object, Type, Type)`. |
| **Fluent Configuration API** | `/Fluent/Overview` | Complete overview of `DefineMap<TSrc, TDest>()` chained member configuration. |
| **Skip & Constant Values** | `/Fluent/SkipAndConstants` | Omitting sensitive fields with `.Skip()` and binding static literals with `.UseValue()`. |
| **Source Redirection** | `/Fluent/MapSource` | Redirecting destination properties to custom computed expressions. |
| **Conditionals & Fallbacks** | `/Fluent/Conditionals` | Executing mappings conditionally (`.If()`) and replacing nulls with fallback defaults (`.Fallback()`). |
| **Bidirectional ReverseMap** | `/Fluent/ReverseMap` | Interactive form demonstrating round-trip bidirectional mappings. |
| **Value Transformers** | `/Fluent/ValueTransformers` | Global string trimming, HTML encoding, and uppercase transformers. |
| **Collections Engine** | `/Collections/Standard` | Mapping arrays and generic lists (`List<T>`). |
| **Sets & Dictionaries** | `/Collections/DictionariesAndSets` | Mapping key-value dictionaries and deduplicated `HashSet<T>` collections. |
| **Null Collection Policies** | `/Collections/NullCollections` | Comparing `AllowNullCollections = false` (empty collection) vs `true` (null). |
| **In-Place Collection Updates**| `/Collections/InPlaceUpdate` | In-place collection synchronization preserving list reference equality. |
| **4-Level Deep Chaining** | `/ComplexTypes/DeepChaining` | Multi-level recursive sub-mapper chaining across organizational graphs. |
| **ConstructUsing Factory** | `/ComplexTypes/ConstructUsing` | Custom instantiation logic for immutable and positional domain types. |
| **Circular Graph Protection** | `/ComplexTypes/CircularReferences`| Safely terminating recursive employee-manager graphs with `MaxDepth(3)`. |
| **Custom Property Resolvers** | `/Resolvers/Index` | Resolving calculated values via Dependency Injection (`IPropertyResolver`). |
| **Type-Level Converters** | `/Resolvers/TypeConverters` | Type-level transformations (`ITypeConverter`, `ConvertUsing`). |
| **Polymorphic Subclasses** | `/Polymorphic/Billing` | Dynamic runtime dispatch for inheritance hierarchies (`Include`, `IncludeBase`). |
| **Lifecycle Callbacks** | `/Lifecycle/Hooks` | Executing `BeforeMap` and `AfterMap` audit hooks. |
| **Configuration Health Check** | `/Diagnostics/HealthCheck` | Automated validation via `AssertConfigurationIsValid()` and `ValidateConfiguration()`. |
| **ORM Query Projections** | `/Projections/Index` | Translating LINQ queries to database SQL with `ProjectOnto<T>()`. |
| **SQL Query Inspector** | `/Projections/SqlInspector` | Side-by-side SQL generation comparisons between in-memory and database projections. |
| **Parameterized Projections** | `/Projections/Filtered` | Passing runtime parameters into SQL queries with plan caching. |
| **Explicit Expansion** | `/Projections/ExplicitExpansion` | Dynamically toggling relation `JOIN` clauses in generated SQL. |
| **Roslyn Analyzers** | `/Migration/Analyzers` | Compile-time diagnostic documentation for `AMS001`, `AMS002`, `AMS003`. |
| **One-Click Code Fixes** | `/Migration/CodeFixes` | Interactive before/after lightbulb code fix demonstrations. |
| **AutoMapper Scanner** | `/Migration/AutoMapperScanner` | Real-time code scanner highlighting legacy AutoMapper syntax. |
| **Migration Workbench** | `/Migration/Playground` | Interactive side-by-side AutoMapper to AutoMapSharp translation workbench. |
| **Performance Benchmarks** | `/Benchmarks/Results` | BenchmarkDotNet throughput and memory allocation comparisons. |
| **Native AOT Diagnostics** | `/Benchmarks/NativeAot` | Cold-start, memory (RSS), and trimming diagnostic dashboard. |
| **Runtime Matrix** | `/About/RuntimeMatrix` | Active CLR diagnostics, assembly metadata, and target framework matrix. |
| **CI/CD Pipeline** | `/About/Pipeline` | GitHub Actions multi-OS matrix and 8-stage verification pipeline. |

---

## How to Run & See the Views

1. Build and pack the local NuGet packages:
   ```bash
   ./scripts/pack-and-test.sh
   ```

2. Run the Web Application:
   ```bash
   cd samples/AutoMapSharp.Sample.Web
   dotnet run
   ```

3. Open your browser and navigate to `http://localhost:5000` (or the URL printed in the terminal console) to interact with the showcase!
