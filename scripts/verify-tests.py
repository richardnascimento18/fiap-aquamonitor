#!/usr/bin/env python3
"""Run each required suite once and verify current-run execution evidence."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--dotnet", default="dotnet", help="Path to the pinned .NET SDK executable")
parser.add_argument("--no-build", action="store_true", help="Use a completed Release build")
parser.add_argument("--results", type=Path, default=ROOT / "TestResults")
args = parser.parse_args()
directory = args.results.resolve()
directory.mkdir(parents=True, exist_ok=True)
# Remove only this runner's generated evidence. Never accept stale reports.
for name in ["domain-architecture.trx", "integration.trx", "contracts.trx", "bdd.trx", "endpoint-manifest.json", "endpoint-coverage.json", "schema-validation.json", "test-summary.json", "bdd-summary.json", "bdd.html", "bdd.ndjson", "evidence-gate-checks.json"]:
    (directory / name).unlink(missing_ok=True)
(directory / "contracts").mkdir(exist_ok=True)
for file in (directory / "contracts").glob("*.json"):
    file.unlink()
bdd_output = ROOT / "AquaMonitor.BddTests/bin/Release/net10.0/TestResults"
for name in ["bdd.html", "bdd.ndjson"]:
    (bdd_output / name).unlink(missing_ok=True)
environment = os.environ | {"AQUA_TEST_RESULTS": str(directory)}


def run(arguments):
    subprocess.run(arguments, cwd=ROOT, env=environment, check=True)


if not args.no_build:
    run([args.dotnet, "restore", "AquaMonitor.sln", "--locked-mode"])
    run([args.dotnet, "build", "AquaMonitor.sln", "--configuration", "Release", "--no-restore", "--warnaserror"])
for name, project, selection in [
    ("domain-architecture", "AquaMonitor.Tests", "Category=Domain|Category=Architecture"),
    ("integration", "AquaMonitor.Tests", "Category!=Domain&Category!=Architecture"),
    ("contracts", "AquaMonitor.ContractTests", None),
    ("bdd", "AquaMonitor.BddTests", None),
]:
    command = [args.dotnet, "test", project, "--configuration", "Release", "--no-build", "--logger", f"trx;LogFileName={name}.trx", "--results-directory", str(directory)]
    if selection:
        command += ["--filter", selection]
    run(command)
for name in ["bdd.html", "bdd.ndjson"]:
    shutil.copy2(bdd_output / name, directory / name)
run([sys.executable, "scripts/ci/verify-test-evidence.py", str(directory), "--check-rejections"])
