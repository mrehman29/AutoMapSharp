#!/usr/bin/env bash
# Builds, tests and packs every package, then pushes them to nuget.org.
#
# Usage:
#   ./scripts/publish-nuget.sh             # pack, test and push
#   ./scripts/publish-nuget.sh --dry-run   # pack and test only, nothing is pushed
#
# The API key is read from NUGET_API_KEY, or from nuget-publish.env in the repository root.
# That file is ignored by git. Copy nuget-publish.env.example to create it.
#
# Prefer the "Publish to NuGet" GitHub workflow for real releases. It builds from a clean checkout,
# so Source Link points at a commit that exists on GitHub.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
PACKAGES_DIR="${ROOT_DIR}/artifacts/packages"
SOURCE="nuget.org"

DRY_RUN=false
if [[ "${1:-}" == "--dry-run" ]]; then
  DRY_RUN=true
fi

ENV_FILE="${ROOT_DIR}/nuget-publish.env"
if [[ -z "${NUGET_API_KEY:-}" && -f "$ENV_FILE" ]]; then
  NUGET_API_KEY=$(sed -n 's/^NUGET_API_KEY=//p' "$ENV_FILE" | tail -1 | tr -d '[:space:]"')
fi

if [[ "$DRY_RUN" == false ]]; then
  if [[ -z "${NUGET_API_KEY:-}" || "$NUGET_API_KEY" == replace-with-* ]]; then
    echo "No NuGet API key found. Put your key in nuget-publish.env, or set NUGET_API_KEY,"
    echo "or pass --dry-run to only build and check the packages."
    exit 1
  fi
  if git -C "$ROOT_DIR" ls-files --error-unmatch nuget-publish.env >/dev/null 2>&1; then
    echo "nuget-publish.env is tracked by git. Remove it from the repository before publishing."
    exit 1
  fi
fi

if [[ "$DRY_RUN" == false && -n "$(git -C "$ROOT_DIR" status --porcelain)" ]]; then
  echo "You have uncommitted changes. Commit and push them first, so the packages point at real source."
  exit 1
fi

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${ROOT_DIR}/Directory.Build.props" | head -1)
echo "=== Preparing AutoMapSharp ${VERSION} ==="

dotnet restore "${ROOT_DIR}/AutoMapSharp.Core.slnf"
dotnet build "${ROOT_DIR}/AutoMapSharp.Core.slnf" -c Release --no-restore
dotnet test "${ROOT_DIR}/tests/AutoMapSharp.Tests/AutoMapSharp.Tests.csproj" -c Release --no-build
"${SCRIPT_DIR}/pack-and-test.sh"

echo ""
echo "=== Packages ready in ${PACKAGES_DIR} ==="
ls -1 "${PACKAGES_DIR}"

if [[ "$DRY_RUN" == true ]]; then
  echo ""
  echo "Dry run: nothing was pushed."
  exit 0
fi

echo ""
echo "=== Pushing to nuget.org ==="
for package in "${PACKAGES_DIR}"/*.nupkg; do
  dotnet nuget push "$package" --api-key "$NUGET_API_KEY" --source "$SOURCE" --skip-duplicate
done

echo ""
echo "Done. New packages can take a few minutes to be validated and indexed on nuget.org."
