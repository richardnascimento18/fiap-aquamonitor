#!/usr/bin/env python3
"""Fail CI for a reported vulnerability or an incomplete NuGet audit."""
import json
import sys
from pathlib import Path


def verify(report):
    if report.get("version") != 1 or not report.get("projects"):
        raise ValueError("NuGet audit did not contain the expected project report.")
    if not {"--vulnerable", "--include-transitive"}.issubset(report.get("parameters", "").split()):
        raise ValueError("NuGet audit must include direct and transitive vulnerability checks.")
    for log in report.get("logs", []):
        if log.get("level", "").lower() in {"error", "warning"}:
            raise ValueError(f"NuGet audit requires review: {log.get('message', 'unknown error')}")
    vulnerable = []
    for project in report["projects"]:
        for framework in project.get("frameworks", []):
            for group in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(group, []):
                    if package.get("vulnerabilities"):
                        vulnerable.append(package["id"])
    if vulnerable:
        raise ValueError("Vulnerable packages: " + ", ".join(sorted(set(vulnerable))))


if __name__ == "__main__":
    try:
        verify(json.loads(Path(sys.argv[1]).read_text()))
    except (ValueError, KeyError, IndexError, OSError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
    print("PASS: No known direct or transitive package vulnerabilities.")
