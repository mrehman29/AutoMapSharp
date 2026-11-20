# Contributing to AutoMapSharp

Thank you for your interest in contributing to AutoMapSharp! Contributions of all kinds are welcome: bug reports, documentation fixes, tests, and code. We expect all participants to collaborate respectfully and constructively.

For questions and bug reports, see [SUPPORT.md](SUPPORT.md). To report a security vulnerability, follow [SECURITY.md](SECURITY.md) — please do not open a public issue.

## Prerequisites & Tooling

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) and [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) installed (CI tests both).
- `Directory.Build.props` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<LangVersion>latest</LangVersion>`. **Any compiler warning fails the build.**

## Project Architecture

- Solution files: `AutoMapSharp.Core.slnf` (daily development filter) and `AutoMapSharp.sln` (full solution).
- Projects:

| Project | Purpose | Target Frameworks |
| :--- | :--- | :--- |
| `src/AutoMapSharp` | Core runtime & expression mapper | `netstandard2.0;net8.0;net9.0` |
| `src/AutoMapSharp.Generators` | Roslyn incremental source generator | `netstandard2.0` |
| `src/AutoMapSharp.Analyzers` | Roslyn analyzers & code fixes | `netstandard2.0` |
| `src/AutoMapSharp.MigrationAnalyzer` | AutoMapper migration analyzers | `netstandard2.0` |
| `src/AutoMapSharp.Collections` | Collection mapping extensions | `netstandard2.0;net8.0;net9.0` |
| `src/AutoMapSharp.Extensions.DependencyInjection` | DI service registration | `netstandard2.0;net8.0;net9.0` |
| `src/AutoMapSharp.Cli` | Command-line tool | `net8.0;net9.0` |

## Local Verification (Matching CI)

Run these from the repository root before opening a pull request:

```bash
# 1. Restore solution dependencies
dotnet restore AutoMapSharp.Core.slnf

# 2. Build with warnings as errors
dotnet build AutoMapSharp.Core.slnf -c Release --no-restore

# 3. Run the unit test suite (Release)
dotnet test tests/AutoMapSharp.Tests/AutoMapSharp.Tests.csproj -c Release --no-build

# 4. Recommended: run the suite in interpreter and generator-disabled modes (CI runs both)
AUTOMAPSHARP_PREFER_INTERPRETATION=1 dotnet test tests/AutoMapSharp.Tests/AutoMapSharp.Tests.csproj -c Release --no-build
AUTOMAPSHARP_DISABLE_GENERATED=1 dotnet test tests/AutoMapSharp.Tests/AutoMapSharp.Tests.csproj -c Release --no-build

# 5. Full package validation script (Bash: Linux, macOS, WSL or Git Bash)
./scripts/pack-and-test.sh
```

> **Warning:** `scripts/pack-and-test.sh` clears cached AutoMapSharp packages from your local NuGet cache (`~/.nuget/packages/automapsharp*` and `/tmp/nuget_packages/automapsharp*`) so the consumer tests use freshly packed builds. It also requires step 1 (restore) to have run first.

CI additionally runs Native AOT validation and benchmark verification (`scripts/check-benchmarks.sh`).

## No Co-Author Attribution Policy (Strict)

- Commits, PR titles, PR descriptions, and PR comments **MUST NOT** include `Co-authored-by:` git trailers or automated assistant attributions.
- Every commit must reflect a single author identity, without multi-author trailers.

Pull requests that do not follow this policy will be asked to rewrite their commit history before merge.

## Public API & Roslyn Tracking Rules

### Public API tracking

`Microsoft.CodeAnalysis.PublicApiAnalyzers` tracks public symbols in:

- `src/AutoMapSharp/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
- `src/AutoMapSharp.Collections/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
- `src/AutoMapSharp.Extensions.DependencyInjection/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`

Any new public symbol must be added to the project's `PublicAPI.Unshipped.txt`. Breaking changes to the public API are not accepted in 1.x without prior discussion in an issue.

### Analyzer release tracking

Changes to analyzer diagnostics (new, changed, or removed rules) must be recorded in:

- `src/AutoMapSharp.Analyzers/AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`
- `src/AutoMapSharp.MigrationAnalyzer/AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`

### XML documentation (`CS1591`)

`GenerateDocumentationFile=true` is enabled in `AutoMapSharp`, `AutoMapSharp.Collections`, and `AutoMapSharp.Extensions.DependencyInjection`. All public types, methods, properties, and constructors in these projects must have XML doc comments (`<summary>`, `<param>`, `<returns>`).

## Pull Request Workflow

1. Fork the repository (or create a branch if you have write access). Suggested branch names: `feature/<issue>_<short_description>` or `fix/<issue>_<short_description>`.
2. Make focused changes with clear commit messages.
3. Open a pull request against `main` that links the related issue (for example, `Fixes #123`) and describes what changed, why, and how it was tested.

### PR checklist

- [ ] Clean build with 0 warnings (`dotnet build AutoMapSharp.Core.slnf -c Release`).
- [ ] Unit tests pass (including interpreter and generator-disabled modes).
- [ ] Documentation and XML doc comments added or updated.
- [ ] `PublicAPI.Unshipped.txt` / `AnalyzerReleases.Unshipped.md` updated if the public surface or diagnostics changed.
- [ ] No `Co-authored-by:` trailers or automated assistant attributions in commits or PR text.
