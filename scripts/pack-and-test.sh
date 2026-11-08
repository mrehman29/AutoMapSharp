#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "=== 1. Building Core, Generators, Analyzers and CLI in Release ==="
dotnet build "${ROOT_DIR}/src/AutoMapSharp.Generators/AutoMapSharp.Generators.csproj" -c Release -m:1 --no-restore
dotnet build "${ROOT_DIR}/src/AutoMapSharp.Analyzers/AutoMapSharp.Analyzers.csproj" -c Release -m:1 --no-restore
dotnet build "${ROOT_DIR}/src/AutoMapSharp.MigrationAnalyzer/AutoMapSharp.MigrationAnalyzer.csproj" -c Release -m:1 --no-restore
dotnet build "${ROOT_DIR}/src/AutoMapSharp/AutoMapSharp.csproj" -c Release -m:1 --no-restore
dotnet build "${ROOT_DIR}/src/AutoMapSharp.Extensions.DependencyInjection/AutoMapSharp.Extensions.DependencyInjection.csproj" -c Release -m:1 --no-restore
dotnet build "${ROOT_DIR}/src/AutoMapSharp.Collections/AutoMapSharp.Collections.csproj" -c Release -m:1 --no-restore
dotnet build "${ROOT_DIR}/src/AutoMapSharp.Cli/AutoMapSharp.Cli.csproj" -c Release -m:1 --no-restore

echo "=== 2. Packing NuGet Packages to artifacts/packages ==="
# Start clean so only packages from this build can be published.
rm -rf "${ROOT_DIR}/artifacts/packages"
mkdir -p "${ROOT_DIR}/artifacts/packages"
dotnet pack "${ROOT_DIR}/src/AutoMapSharp/AutoMapSharp.csproj" -c Release -o "${ROOT_DIR}/artifacts/packages" -m:1 --no-restore --no-build
dotnet pack "${ROOT_DIR}/src/AutoMapSharp.Extensions.DependencyInjection/AutoMapSharp.Extensions.DependencyInjection.csproj" -c Release -o "${ROOT_DIR}/artifacts/packages" -m:1 --no-restore --no-build
dotnet pack "${ROOT_DIR}/src/AutoMapSharp.MigrationAnalyzer/AutoMapSharp.MigrationAnalyzer.csproj" -c Release -o "${ROOT_DIR}/artifacts/packages" -m:1 --no-restore --no-build
dotnet pack "${ROOT_DIR}/src/AutoMapSharp.Collections/AutoMapSharp.Collections.csproj" -c Release -o "${ROOT_DIR}/artifacts/packages" -m:1 --no-restore --no-build
dotnet pack "${ROOT_DIR}/src/AutoMapSharp.Cli/AutoMapSharp.Cli.csproj" -c Release -o "${ROOT_DIR}/artifacts/packages" -m:1 --no-restore --no-build

echo "=== 3. Clearing Local Cache for AutoMapSharp ==="
rm -rf ~/.nuget/packages/automapsharp* || true
rm -rf /tmp/nuget_packages/automapsharp* || true

echo "=== 4. Restoring NugetConsumer with Local Packages ==="
dotnet restore "${ROOT_DIR}/tests/AutoMapSharp.IntegrationTests.NugetConsumer/AutoMapSharp.IntegrationTests.NugetConsumer.csproj" --source "${ROOT_DIR}/artifacts/packages" --source "https://api.nuget.org/v3/index.json"

echo "=== 5. Running NugetConsumer Tests ==="
dotnet test "${ROOT_DIR}/tests/AutoMapSharp.IntegrationTests.NugetConsumer/AutoMapSharp.IntegrationTests.NugetConsumer.csproj" -m:1 --no-restore

echo "=== 5b. Running NugetConsumer Tests with generated code disabled ==="
AUTOMAPSHARP_DISABLE_GENERATED=1 dotnet test "${ROOT_DIR}/tests/AutoMapSharp.IntegrationTests.NugetConsumer/AutoMapSharp.IntegrationTests.NugetConsumer.csproj" -m:1 --no-restore --no-build \
  --filter "FullyQualifiedName!~SourceGenerator_EmitsMapsThatTheEngineUses"

echo "=== 6. Restoring and Building Sample Web App with Local Packages ==="
dotnet restore "${ROOT_DIR}/samples/AutoMapSharp.Sample.Web/AutoMapSharp.Sample.Web.csproj" --source "${ROOT_DIR}/artifacts/packages" --source "https://api.nuget.org/v3/index.json"
dotnet build "${ROOT_DIR}/samples/AutoMapSharp.Sample.Web/AutoMapSharp.Sample.Web.csproj" -m:1 --no-restore

echo "=== SUCCESS: All Tests Passed and Sample Web Application Built Cleanly! ==="
