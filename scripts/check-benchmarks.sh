#!/usr/bin/env bash
# Fails when AutoMapSharp is more than MAX_RATIO times slower than hand-written code in the same run.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ARTIFACTS="${ROOT_DIR}/artifacts/benchmarks"
MAX_RATIO="${MAX_RATIO:-4.0}"

rm -rf "${ARTIFACTS}"
dotnet run -c Release -f net8.0 --project "${ROOT_DIR}/benchmarks/AutoMapSharp.Benchmarks" -- \
  --run --job short --filter "*" --artifacts "${ARTIFACTS}" > "${ARTIFACTS}.log" 2>&1 || { tail -50 "${ARTIFACTS}.log"; exit 1; }

python3 - "${ARTIFACTS}/results" "${MAX_RATIO}" <<'PY'
import csv, glob, sys
folder, limit = sys.argv[1], float(sys.argv[2])
failed = False
for path in sorted(glob.glob(folder + "/*-report.csv")):
    with open(path, newline="") as f:
        for row in csv.DictReader(f):
            if row.get("Method") != "AutoMapSharp":
                continue
            ratio = float(row["Ratio"])
            scenario = path.split("Benchmarks.")[-1].replace("-report.csv", "")
            status = "OK" if ratio <= limit else "TOO SLOW"
            failed |= ratio > limit
            print(f"{scenario:32} AutoMapSharp / hand-written = {ratio:6.2f}  (limit {limit})  {status}")
sys.exit(1 if failed else 0)
PY
