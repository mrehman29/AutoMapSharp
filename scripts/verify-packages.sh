#!/usr/bin/env bash
# scripts/verify-packages.sh
# Comprehensive package verification & consumer integration suite for AutoMapSharp.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${ROOT_DIR}"

PACKAGES_DIR="${ROOT_DIR}/artifacts/packages"

# Preflight: fail fast before a full pack if required tools are missing
for required_cmd in python3 dotnet git; do
  if ! command -v "${required_cmd}" >/dev/null 2>&1; then
    echo "ERROR: Required command '${required_cmd}' not found on PATH."
    exit 1
  fi
done

# Portable SHA-256 (sha256sum on minimal Linux images, shasum on macOS)
compute_sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  else
    shasum -a 256 "$1" | awk '{print $1}'
  fi
}

# Private, empty NuGet global-packages folder: guarantees fresh package resolution
# without touching user/CI caches or relying on world-writable shared paths.
NUGET_CACHE_DIR=$(mktemp -d "${TMPDIR:-/tmp}/automapsharp_nuget_XXXXXX")
export NUGET_PACKAGES="${NUGET_CACHE_DIR}"
trap 'rm -rf "${TOOL_TEST_DIR:-}" "${TRX_DIR:-}" "${NUGET_CACHE_DIR:-}"' EXIT

echo "================================================================="
echo "=== Step 0: Checking Clean Tracked Working Tree & Git HEAD ==="
echo "================================================================="

# Check tracked changes only, ignoring untracked editor/agent files
TRACKED_DIRTY=$(git status --porcelain --untracked-files=no)
if [[ -n "${TRACKED_DIRTY}" ]]; then
  if [[ "${ALLOW_DIRTY:-0}" == "1" ]]; then
    echo "WARNING: Working tree has uncommitted tracked changes, continuing due to ALLOW_DIRTY=1."
  else
    echo "ERROR: Working tree has uncommitted tracked changes. Commit them locally first so Source Link matches HEAD commit:"
    echo "${TRACKED_DIRTY}"
    exit 1
  fi
fi

HEAD_COMMIT_SHA=$(git rev-parse HEAD)
export EXPECTED_COMMIT_SHA="${HEAD_COMMIT_SHA}"
echo "Current Git HEAD commit: ${HEAD_COMMIT_SHA}"

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${ROOT_DIR}/Directory.Build.props" | head -1 | tr -d '[:space:]')
export EXPECTED_PACKAGE_VERSION="${VERSION}"
echo "Detected AutoMapSharp version: ${VERSION}"

echo ""
echo "================================================================="
echo "=== Step 1: Restoring (private NuGet cache) and Packing ==="
echo "================================================================="
echo "Using private NuGet global-packages folder: ${NUGET_PACKAGES}"

# Restore solution first so fresh checkouts do not fail on --no-restore builds
dotnet restore "${ROOT_DIR}/AutoMapSharp.Core.slnf"
"${SCRIPT_DIR}/pack-and-test.sh"

echo ""
echo "================================================================="
echo "=== Step 2: Validating Generated Package Artifacts ==="
echo "================================================================="

EXPECTED_PACKAGES=(
  "AutoMapSharp.${VERSION}.nupkg"
  "AutoMapSharp.Collections.${VERSION}.nupkg"
  "AutoMapSharp.Extensions.DependencyInjection.${VERSION}.nupkg"
  "AutoMapSharp.MigrationAnalyzer.${VERSION}.nupkg"
  "dotnet-automapsharp-migrate.${VERSION}.nupkg"
)

EXPECTED_SNUPKGS=(
  "AutoMapSharp.${VERSION}.snupkg"
  "AutoMapSharp.Collections.${VERSION}.snupkg"
  "AutoMapSharp.Extensions.DependencyInjection.${VERSION}.snupkg"
)

for pkg in "${EXPECTED_PACKAGES[@]}"; do
  PKG_PATH="${PACKAGES_DIR}/${pkg}"
  if [[ ! -f "${PKG_PATH}" ]]; then
    echo "ERROR: Missing expected package ${PKG_PATH}"
    exit 1
  fi
  SIZE=$(wc -c < "${PKG_PATH}" | tr -d '[:space:]')
  echo "  [OK] Found nupkg: ${pkg} (${SIZE} bytes)"
done

for snupkg in "${EXPECTED_SNUPKGS[@]}"; do
  SNUPKG_PATH="${PACKAGES_DIR}/${snupkg}"
  if [[ ! -f "${SNUPKG_PATH}" ]]; then
    echo "ERROR: Missing expected symbol package ${SNUPKG_PATH}"
    exit 1
  fi
  SIZE=$(wc -c < "${SNUPKG_PATH}" | tr -d '[:space:]')
  echo "  [OK] Found snupkg: ${snupkg} (${SIZE} bytes)"
done

echo ""
echo "================================================================="
echo "=== Step 3: Validating .snupkg Symbol Packages & Source Link ==="
echo "================================================================="

python3 - "${PACKAGES_DIR}" "${HEAD_COMMIT_SHA}" << 'EOF'
import zipfile
import sys
import os
import glob
import re

packages_dir = sys.argv[1]
head_sha = sys.argv[2]
snupkgs = glob.glob(os.path.join(packages_dir, "*.snupkg"))

if not snupkgs:
    print(f"ERROR: No .snupkg packages found in {packages_dir}")
    sys.exit(1)

expected_tfms = ["net8.0", "net9.0", "netstandard2.0"]
has_error = False
sl_pattern = re.compile(r"https://raw\.githubusercontent\.com/mrehman29/AutoMapSharp/([0-9a-fA-F]{40})/")

for snupkg_path in snupkgs:
    pkg_name = os.path.basename(snupkg_path)
    print(f"\nChecking symbol package: {pkg_name}")
    with zipfile.ZipFile(snupkg_path, 'r') as zf:
        namelist = zf.namelist()
        
        for tfm in expected_tfms:
            matching = [n for n in namelist if f"lib/{tfm}/" in n and n.endswith(".pdb")]
            if not matching:
                print(f"  ERROR: {pkg_name} is missing PDB for {tfm}")
                has_error = True
            else:
                pdb_name = matching[0]
                pdb_bytes = zf.read(pdb_name)
                # Verify Portable PDB signature (BSJB = 0x42534A42)
                if not pdb_bytes.startswith(b"BSJB"):
                    print(f"  ERROR: {pdb_name} in {pkg_name} is not a valid Portable PDB (missing BSJB signature).")
                    has_error = True
                else:
                    print(f"  [OK] {tfm} contains valid Portable PDB ({pdb_name}, {len(pdb_bytes)} bytes)")
                
                # Extract and verify Source Link JSON metadata
                match = sl_pattern.search(pdb_bytes.decode('utf-8', errors='ignore'))
                if match:
                    embedded_sha = match.group(1)
                    if embedded_sha.lower() == head_sha.lower():
                        print(f"  [OK] {pdb_name} Source Link commit SHA matches HEAD ({embedded_sha[:8]})")
                    else:
                        print(f"  ERROR: {pdb_name} Source Link commit SHA '{embedded_sha}' does not match HEAD '{head_sha}'")
                        has_error = True
                else:
                    print(f"  ERROR: {pdb_name} in {pkg_name} is missing Source Link URL mapping.")
                    has_error = True

if has_error:
    sys.exit(1)
print("\nAll .snupkg symbol packages and Source Link records verified successfully!")
EOF

echo ""
echo "================================================================="
echo "=== Step 4: Validating .nupkg Manifest Metadata & Tool PDBs ==="
echo "================================================================="

python3 - "${PACKAGES_DIR}" "${HEAD_COMMIT_SHA}" "${VERSION}" << 'EOF'
import zipfile
import sys
import os
import glob
import re
import xml.etree.ElementTree as ET

packages_dir = sys.argv[1]
head_sha = sys.argv[2]
version = sys.argv[3]
nupkgs = glob.glob(os.path.join(packages_dir, "*.nupkg"))

has_error = False
sl_pattern = re.compile(r"https://raw\.githubusercontent\.com/mrehman29/AutoMapSharp/([0-9a-fA-F]{40})/")

for nupkg_path in nupkgs:
    pkg_name = os.path.basename(nupkg_path)
    pkg_has_error = False
    print(f"\nInspecting package: {pkg_name}")
    with zipfile.ZipFile(nupkg_path, 'r') as zf:
        names = zf.namelist()
        
        # Check icon and readme
        if "icon.png" not in names:
            print(f"  ERROR: {pkg_name} missing icon.png")
            pkg_has_error = True
        else:
            print(f"  [OK] Embedded icon.png present")
            
        if "README.md" not in names:
            print(f"  ERROR: {pkg_name} missing README.md")
            pkg_has_error = True
        else:
            print(f"  [OK] Embedded README.md present")

        # Find nuspec
        nuspecs = [n for n in names if n.endswith(".nuspec")]
        if not nuspecs:
            print(f"  ERROR: {pkg_name} missing .nuspec")
            pkg_has_error = True
            has_error = True
            continue
            
        nuspec_content = zf.read(nuspecs[0])
        try:
            root = ET.fromstring(nuspec_content)
        except ET.ParseError as e:
            print(f"  ERROR: {pkg_name} has malformed .nuspec XML: {e}")
            pkg_has_error = True
            has_error = True
            continue
        # Strip XML namespaces to support both 2012/06 and 2013/05 nuspec schemas
        for elem in root.iter():
            if "}" in elem.tag:
                elem.tag = elem.tag.split("}", 1)[1]

        meta = root.find("metadata")
        license_elem = meta.find("license") if meta is not None else None
        if license_elem is None or license_elem.text != "MIT":
            print(f"  ERROR: {pkg_name} invalid or missing license expression (expected MIT)")
            pkg_has_error = True
        else:
            print(f"  [OK] License: {license_elem.text}")

        repo_elem = meta.find("repository") if meta is not None else None
        if repo_elem is None or "AutoMapSharp" not in repo_elem.attrib.get('url', ''):
            print(f"  ERROR: {pkg_name} missing or invalid repository url")
            pkg_has_error = True
        else:
            print(f"  [OK] Repository URL: {repo_elem.attrib.get('url')} (commit: {repo_elem.attrib.get('commit', 'none')})")

        # Specific package checks
        if pkg_name == f"AutoMapSharp.{version}.nupkg":
            if "analyzers/dotnet/cs/AutoMapSharp.Generators.dll" not in names:
                print(f"  ERROR: {pkg_name} missing analyzers/dotnet/cs/AutoMapSharp.Generators.dll")
                pkg_has_error = True
            if "analyzers/dotnet/cs/AutoMapSharp.Analyzers.dll" not in names:
                print(f"  ERROR: {pkg_name} missing analyzers/dotnet/cs/AutoMapSharp.Analyzers.dll")
                pkg_has_error = True
            if not pkg_has_error:
                print(f"  [OK] Source generator and analyzers bundled in analyzers/dotnet/cs/")

        # Dotnet tool checks: net8.0 AND net9.0
        if "dotnet-automapsharp-migrate" in pkg_name:
            for tfm in ["net8.0", "net9.0"]:
                settings_file = f"tools/{tfm}/any/DotnetToolSettings.xml"
                if settings_file not in names:
                    print(f"  ERROR: {pkg_name} missing {settings_file}")
                    pkg_has_error = True
                else:
                    print(f"  [OK] Found {settings_file}")

                for pdb_base in ["AutoMapSharp.Cli.pdb", "AutoMapSharp.MigrationAnalyzer.pdb"]:
                    pdb_path = f"tools/{tfm}/any/{pdb_base}"
                    if pdb_path not in names:
                        print(f"  ERROR: {pkg_name} missing tool PDB {pdb_path}")
                        pkg_has_error = True
                    else:
                        pdb_bytes = zf.read(pdb_path)
                        if not pdb_bytes.startswith(b"BSJB"):
                            print(f"  ERROR: Tool PDB {pdb_path} is missing BSJB signature")
                            pkg_has_error = True
                        else:
                            match = sl_pattern.search(pdb_bytes.decode('utf-8', errors='ignore'))
                            if match and match.group(1).lower() == head_sha.lower():
                                print(f"  [OK] Tool PDB {pdb_path} valid Portable PDB with matching Source Link SHA")
                            else:
                                print(f"  ERROR: Tool PDB {pdb_path} Source Link invalid or mismatched")
                                pkg_has_error = True

    if pkg_has_error:
        has_error = True

if has_error:
    sys.exit(1)
print("\nAll .nupkg package manifests and tool PDBs verified successfully!")
EOF

echo ""
echo "================================================================="
echo "=== Step 5: Validating Global Tool Local Installation ==="
echo "================================================================="

TOOL_TEST_DIR=$(mktemp -d "${TMPDIR:-/tmp}/automapsharp_tool_test_XXXXXX")
TRX_DIR=$(mktemp -d "${TMPDIR:-/tmp}/automapsharp_trx_XXXXXX")

# Isolated feed: only the freshly packed artifacts, so nuget.org copies of the same version can never be installed
cat << XML > "${TOOL_TEST_DIR}/nuget.config"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="${PACKAGES_DIR}" />
  </packageSources>
</configuration>
XML

echo "Installing dotnet-automapsharp-migrate into temporary directory ${TOOL_TEST_DIR}..."
dotnet tool install --tool-path "${TOOL_TEST_DIR}" \
  --configfile "${TOOL_TEST_DIR}/nuget.config" \
  dotnet-automapsharp-migrate \
  --version "${VERSION}"

# Assert provenance: installed package must be byte-identical to the freshly packed artifact
LOCAL_TOOL_NUPKG="${PACKAGES_DIR}/dotnet-automapsharp-migrate.${VERSION}.nupkg"
INSTALLED_TOOL_NUPKG=$(find "${TOOL_TEST_DIR}/.store" -type f -iname "dotnet-automapsharp-migrate.${VERSION}.nupkg" | head -1)
if [[ -z "${INSTALLED_TOOL_NUPKG}" ]]; then
  echo "ERROR: Installed tool nupkg not found under ${TOOL_TEST_DIR}/.store"
  exit 1
fi
LOCAL_TOOL_SHA=$(compute_sha256 "${LOCAL_TOOL_NUPKG}")
INSTALLED_TOOL_SHA=$(compute_sha256 "${INSTALLED_TOOL_NUPKG}")
if [[ "${LOCAL_TOOL_SHA}" != "${INSTALLED_TOOL_SHA}" ]]; then
  echo "ERROR: Installed tool package (${INSTALLED_TOOL_SHA}) does not match freshly packed artifact (${LOCAL_TOOL_SHA})"
  exit 1
fi
echo "  [OK] Installed tool package matches freshly packed artifact (sha256 ${LOCAL_TOOL_SHA:0:12})"

echo "Executing tool --help..."
"${TOOL_TEST_DIR}/automapsharp-migrate" --help

echo "Testing dry-run invocation and verifying non-destructive behavior..."
mkdir -p "${TOOL_TEST_DIR}/sample_src"
SAMPLE_FILE="${TOOL_TEST_DIR}/sample_src/TestMapping.cs"
cat << 'CS' > "${SAMPLE_FILE}"
using AutoMapper;

public class OldProfile : Profile
{
    public OldProfile()
    {
        CreateMap<int, string>();
    }
}
CS

ORIGINAL_HASH=$(compute_sha256 "${SAMPLE_FILE}")

if ! DRY_RUN_OUTPUT=$("${TOOL_TEST_DIR}/automapsharp-migrate" "${TOOL_TEST_DIR}/sample_src" --dry-run 2>&1); then
  echo "ERROR: Tool dry-run invocation failed with exit code non-zero:"
  echo "${DRY_RUN_OUTPUT}"
  exit 1
fi

POST_RUN_HASH=$(compute_sha256 "${SAMPLE_FILE}")

# 1. Assert sample file is bit-for-bit unchanged
if [[ "${ORIGINAL_HASH}" != "${POST_RUN_HASH}" ]]; then
  echo "ERROR: --dry-run modified sample source file on disk!"
  exit 1
fi
echo "  [OK] Source file remained bit-for-bit identical after --dry-run"

# 2. Assert dry-run output contains expected rewrite notifications and rules
if grep -q '\[MODIFIED\]' <<< "${DRY_RUN_OUTPUT}" && \
   grep -q 'AMSMIG001' <<< "${DRY_RUN_OUTPUT}" && \
   grep -q 'AMSMIG002' <<< "${DRY_RUN_OUTPUT}" && \
   grep -q 'AMSMIG003' <<< "${DRY_RUN_OUTPUT}"; then
  echo "  [OK] Tool dry-run output correctly identified modifications and diagnostic rules AMSMIG001/2/3"
else
  echo "ERROR: Tool output missing expected [MODIFIED] or AMSMIG001/2/3 notification:"
  echo "${DRY_RUN_OUTPUT}"
  exit 1
fi

echo ""
echo "================================================================="
echo "=== Step 6: Multi-TFM Consumer Tests Execution & TRX Audit ==="
echo "================================================================="

CONSUMER_CSPROJ="${ROOT_DIR}/tests/AutoMapSharp.IntegrationTests.NugetConsumer/AutoMapSharp.IntegrationTests.NugetConsumer.csproj"

for tfm in "net8.0" "net9.0"; do
  echo "Running PackageVerificationTests on ${tfm} in Generated Mode..."
  dotnet test "${CONSUMER_CSPROJ}" -f "${tfm}" \
    --filter "FullyQualifiedName~PackageVerificationTests" \
    -m:1 --no-restore --no-build \
    --blame-hang-timeout 5m \
    --logger "trx;LogFileName=${TRX_DIR}/pv_generated_${tfm}.trx"

  echo "Running PackageVerificationTests on ${tfm} in Fallback Mode..."
  AUTOMAPSHARP_DISABLE_GENERATED=1 dotnet test "${CONSUMER_CSPROJ}" -f "${tfm}" \
    --filter "FullyQualifiedName~PackageVerificationTests" \
    -m:1 --no-restore --no-build \
    --blame-hang-timeout 5m \
    --logger "trx;LogFileName=${TRX_DIR}/pv_fallback_${tfm}.trx"
done

# Validate TRX test results across all 4 matrix runs
python3 - "${TRX_DIR}" << 'EOF'
import xml.etree.ElementTree as ET
import glob
import sys
import os

trx_dir = sys.argv[1]
expected_runs = [
    "pv_generated_net8.0.trx",
    "pv_generated_net9.0.trx",
    "pv_fallback_net8.0.trx",
    "pv_fallback_net9.0.trx"
]

has_error = False

for run_file in expected_runs:
    trx_path = os.path.join(trx_dir, run_file)
    if not os.path.isfile(trx_path):
        print(f"ERROR: Missing TRX output file: {run_file}")
        has_error = True
        continue

    tree = ET.parse(trx_path)
    root = tree.getroot()
    summary = root.find('{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}ResultSummary')
    if summary is None:
        summary = root.find('ResultSummary')
        
    counters = summary.find('{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters') if summary is not None else None
    if counters is None and summary is not None:
        counters = summary.find('Counters')

    total = int(counters.attrib.get('total', '0')) if counters is not None else 0
    passed = int(counters.attrib.get('passed', '0')) if counters is not None else 0
    failed = int(counters.attrib.get('failed', '0')) if counters is not None else 0
    outcome = summary.attrib.get('outcome', 'Failed') if summary is not None else 'Failed'
    
    print(f"TRX Audit: {run_file} -> Outcome: {outcome}, Total: {total}, Passed: {passed}, Failed: {failed}")
    # vstest marks successful runs as "Completed" (or "Passed")
    if total != 4 or passed != 4 or failed != 0 or outcome not in ("Completed", "Passed"):
        print(f"ERROR: Expected total=4, passed=4, failed=0, outcome in (Completed, Passed) in {run_file}, got total={total}, passed={passed}, failed={failed}, outcome={outcome}")
        has_error = True

if has_error:
    sys.exit(1)

print("\nAll 4 Multi-TFM PackageVerificationTests matrix runs passed with 100% success!")
EOF

echo ""
echo "================================================================="
echo "=== SUCCESS: All 5 Packages, Symbols, Source Link & Consumer Tests Passed! ==="
echo "================================================================="
