#!/usr/bin/env python3
"""Fail closed on missing execution, incomplete endpoint evidence, or BDD steps."""
import argparse
import json
import re
from pathlib import Path
import shutil
import tempfile
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
ROOT = Path(__file__).resolve().parents[2]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_trx(path):
    root = ET.parse(path).getroot()
    counters = root.find("t:ResultSummary/t:Counters", NS)
    require(counters is not None, f"No counters: {path}")
    require(int(counters.get("total", 0)) > 0, f"Empty test selection: {path}")
    definitions = {}
    for test in root.findall("t:TestDefinitions/t:UnitTest", NS):
        method = test.find("t:TestMethod", NS)
        definitions[test.get("id")] = method.get("className") + "." + method.get("name")
    results = []
    for result in root.findall("t:Results/t:UnitTestResult", NS):
        require(result.get("outcome") == "Passed", f"Non-passing test: {result.get('testName')}")
        results.append({"test": definitions[result.get("testId")], "name": result.get("testName"), "status": result.get("outcome")})
    require(len(results) == int(counters.get("total")) == int(counters.get("passed")), f"Incomplete TRX execution: {path}")
    require(len({r["name"] for r in results}) == len(results), f"Duplicate test execution: {path}")
    return results


def verify_bdd(path, expected):
    messages = [json.loads(line) for line in path.read_text().splitlines() if line.strip()]
    def items(key):
        return [message[key] for message in messages if key in message]
    pickles = {p["id"]: p for p in items("pickle")}
    cases = {c["id"]: c for c in items("testCase")}
    starts = items("testCaseStarted")
    finishes = items("testCaseFinished")
    steps = items("testStepFinished")
    require(len(pickles) >= expected, "Missing expected BDD examples")
    require(len(pickles) == len(cases) == len(starts) == len(finishes), "BDD examples were not each executed exactly once")
    require({c["pickleId"] for c in cases.values()} == set(pickles), "Unexecuted Gherkin examples")
    require({s["testCaseId"] for s in starts} == set(cases), "Missing BDD test cases")
    require({f["testCaseStartedId"] for f in finishes} == {s["id"] for s in starts}, "Unfinished BDD scenarios")
    require(all(not f.get("willBeRetried") for f in finishes), "Unexpected BDD retry")
    summary = []
    for start in starts:
        case = cases[start["testCaseId"]]
        actual = [s for s in steps if s["testCaseStartedId"] == start["id"]]
        require(len(actual) == len(case["testSteps"]), "Missing BDD step execution")
        require({s["testStepId"] for s in actual} == {s["id"] for s in case["testSteps"]}, "Missing BDD step results")
        require(all(s["testStepResult"]["status"] == "PASSED" for s in actual), "Undefined, pending, skipped, or failed BDD step")
        pickle = pickles[case["pickleId"]]
        summary.append({"feature": pickle["uri"], "scenario": pickle["name"], "status": "Passed", "steps": len(pickle["steps"]), "exampleId": pickle["id"]})
    runs = items("testRunFinished")
    require(len(runs) == 1 and runs[0]["success"], "BDD run did not finish successfully")
    return summary


def verify(directory):
    requirements = json.loads((ROOT / "AquaMonitor.ContractTests/evidence-requirements.json").read_text())
    suites = {name: read_trx(directory / (name + ".trx")) for name in requirements["suites"]}
    for name, minimum in requirements["suites"].items():
        require(len(suites[name]) >= minimum, f"Suite {name} ran fewer than {minimum} tests")
    require(len(suites["domain-architecture"]) + len(suites["integration"]) >= requirements["existingRegressions"], "Existing regression execution fell below the verified baseline")
    all_results = [result for results in suites.values() for result in results]
    require(len({r["name"] for r in all_results}) == len(all_results), "Test suites executed overlapping tests")
    contract_methods = {r["test"] for r in suites["contracts"]}
    inventory = json.loads((ROOT / "AquaMonitor.ContractTests/endpoint-coverage.json").read_text())["operations"]
    manifest = json.loads((directory / "endpoint-manifest.json").read_text())
    require(len(manifest) == 2 * len(inventory), "Incomplete runtime endpoint manifest")
    require(len({(m["id"], m["outcome"]) for m in manifest}) == len(manifest), "Duplicate manifest entries")
    require(any(r["test"].endswith("EndpointCoverageTests.RuntimeOperations_MatchInventory_AndHaveExecutablePositiveAndNegativeContracts") for r in suites["contracts"]), "Runtime discovery guard did not execute")
    summary = []
    for operation in inventory:
        row = {"operationId": operation["id"], "method": operation["method"], "route": operation["route"]}
        for outcome in ["positive", "negative"]:
            expected = operation[outcome]
            matches = [m for m in manifest if m["id"] == operation["id"] and m["outcome"] == outcome]
            require(len(matches) == 1, f"Missing manifest entry: {operation['id']} {outcome}")
            metadata = matches[0]
            require(metadata["method"] == operation["method"] and metadata["route"] == operation["route"], "Stale route metadata")
            require(metadata["test"].split(".")[-1] == expected["test"], "Wrong test association")
            require(metadata["test"] in contract_methods, f"Required test never passed: {metadata['test']}")
            evidence = json.loads((directory / "contracts" / f"{operation['id']}-{outcome}.json").read_text())
            require(evidence["operationId"] == operation["id"] and evidence["outcome"] == outcome, "Wrong evidence association")
            require(evidence["test"] == metadata["test"], "Wrong executed evidence test")
            require(evidence["requestMethod"] == operation["method"], "Evidence came from the wrong HTTP method")
            pattern = re.escape(operation["route"]).replace(re.escape("{id:int}"), r"-?\d+")
            require(re.fullmatch(pattern, evidence["requestPath"]), "Evidence came from the wrong route")
            require(evidence["status"] == metadata["status"] == expected["status"], "Status evidence mismatch")
            require(evidence["schema"] == metadata["schema"] == expected["schema"], "Schema evidence mismatch")
            require(evidence["emptyBodyVerified"] == metadata["emptyBody"] == expected["emptyBody"], "Empty-body evidence mismatch")
            if expected["schema"]:
                require(evidence["schemaValidated"] and evidence["jsonBodyVerified"], "JSON schema/body never validated")
            else:
                require(expected["emptyBody"] and not evidence["schemaValidated"] and not evidence["jsonBodyVerified"], "Unjustified missing schema")
            row[outcome] = {"status": "Passed", "httpStatus": evidence["status"], "test": evidence["test"], "jsonBody": "Verified" if evidence["jsonBodyVerified"] else "N/A (empty response verified)", "jsonSchema": evidence["schema"] or "N/A (empty response verified)"}
        summary.append(row)
    require(len(list((directory / "contracts").glob("*.json"))) == len(manifest), "Stale or extra response evidence")
    schemas = sorted((ROOT / "AquaMonitor.ContractTests/Schemas").glob("*.schema.json"))
    require(len(schemas) >= requirements["schemas"], "Missing schemas")
    schema_evidence = []
    for schema in schemas:
        name = schema.name.removesuffix(".schema.json")
        result = [r for r in suites["contracts"] if r["test"].endswith("SchemaTests.Schema_ValidatesDialectAndSample_RejectsMissingAndWrongTypes") and f'name: "{name}"' in r["name"]]
        require(len(result) == 1, f"Schema self-validation never passed: {name}")
        schema_evidence.append({"schema": schema.name, "dialect": "2020-12", "metaSchema": "Passed", "validSample": "Passed", "missingPropertyRejected": True, "wrongTypeRejected": True})
    bdd = verify_bdd(directory / "bdd.ndjson", requirements["suites"]["bdd"])
    require(len(bdd) == len(suites["bdd"]), "BDD messages and TRX counts disagree")
    require(len({r["feature"] for r in bdd}) == len(list((ROOT / "AquaMonitor.BddTests/Features").glob("*.feature"))), "Missing BDD feature execution")
    require((directory / "bdd.html").stat().st_size > 1000, "Missing BDD HTML report")
    for filename, data in [("endpoint-coverage.json", summary), ("schema-validation.json", schema_evidence), ("bdd-summary.json", bdd), ("test-summary.json", {name: {"passed": len(results), "failed": 0, "skipped": 0} for name, results in suites.items()})]:
        (directory / filename).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    print(f"Verified {len(all_results)} passing tests, {len(inventory)} complete operations, {len(schemas)} schemas, {len(bdd)} BDD examples; zero failed/skipped tests.")


def check_rejections(directory):
    """Exercise the gate with corrupted copies of real current-run evidence."""
    def edit_json(path, mutate):
        value = json.loads(path.read_text())
        mutate(value)
        path.write_text(json.dumps(value))

    def outcome(path, value):
        tree = ET.parse(path)
        tree.getroot().find("t:Results/t:UnitTestResult", NS).set("outcome", value)
        tree.write(path)

    def undefined_step(path):
        messages = [json.loads(line) for line in path.read_text().splitlines()]
        next(m["testStepFinished"] for m in messages if "testStepFinished" in m)["testStepResult"]["status"] = "UNDEFINED"
        path.write_text("\n".join(json.dumps(m) for m in messages))

    mutations = {
        "missing suite": lambda p: (p / "bdd.trx").unlink(),
        "failed test": lambda p: outcome(p / "contracts.trx", "Failed"),
        "skipped test": lambda p: outcome(p / "bdd.trx", "NotExecuted"),
        "missing response verification": lambda p: (p / "contracts/AUTH-01-positive.json").unlink(),
        "missing schema verification": lambda p: edit_json(p / "contracts/AUTH-01-positive.json", lambda value: value.update(schemaValidated=False)),
        "missing route coverage": lambda p: edit_json(p / "endpoint-manifest.json", lambda value: value.pop()),
        "wrong test association": lambda p: edit_json(p / "contracts/AUTH-01-positive.json", lambda value: value.update(test="Unexecuted.Test")),
        "undefined Gherkin step": lambda p: undefined_step(p / "bdd.ndjson"),
        "stale response evidence": lambda p: (p / "contracts/STALE-positive.json").write_text("{}"),
    }
    results = []
    with tempfile.TemporaryDirectory(prefix="aquamonitor-evidence-gate-") as temporary:
        for index, (name, mutation) in enumerate(mutations.items()):
            copy = Path(temporary) / str(index)
            shutil.copytree(directory, copy)
            mutation(copy)
            try:
                verify(copy)
            except (ValueError, KeyError, OSError, ET.ParseError):
                results.append({"failure": name, "rejected": True})
            else:
                raise ValueError(f"Evidence gate accepted {name}")
    (directory / "evidence-gate-checks.json").write_text(json.dumps(results, indent=2) + "\n")
    print(f"Verified rejection of {len(results)} corrupted/missing evidence conditions.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--check-rejections", action="store_true")
    args = parser.parse_args()
    try:
        verify(args.directory)
        if args.check_rejections:
            check_rejections(args.directory)
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        raise SystemExit(f"Test evidence rejected: {error}") from error
