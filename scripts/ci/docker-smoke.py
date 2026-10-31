#!/usr/bin/env python3
"""Production smoke test shared by local verification and GitHub Actions.

Usage: python3 scripts/ci/docker-smoke.py --image aquamonitor:ci
Requires Python 3, Docker, and an already-built AquaMonitor image.
"""

import argparse
import json
import os
import secrets
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def docker(*arguments, timeout=30, environment=None):
    return subprocess.run(
        ["docker", *arguments], check=True, capture_output=True, text=True,
        timeout=timeout, env={**os.environ, **(environment or {})},
    ).stdout.strip()


def request(base_url, path, expected=200, payload=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = json.dumps(payload).encode() if payload is not None else None
    try:
        with urllib.request.urlopen(
            urllib.request.Request(base_url + path, data=data, headers=headers), timeout=5
        ) as response:
            require(response.status == expected, f"Unexpected HTTP status for {path}")
            return json.load(response)
    except urllib.error.HTTPError as error:
        require(error.code == expected, f"Expected HTTP {expected}, got {error.code} for {path}")
        return None


def ready(container):
    # Docker can assign a different ephemeral host port after a restart.
    port = docker("port", container, "8080/tcp").rsplit(":", 1)[1]
    base_url = "http://127.0.0.1:" + port
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        require(docker("inspect", "--format", "{{.State.Running}}", container) == "true",
                "Container exited before becoming ready")
        try:
            with urllib.request.urlopen(base_url, timeout=2) as response:
                if response.status == 200 and json.load(response).get("api") == "AquaMonitor API v1":
                    return base_url
        except (OSError, ValueError):
            pass  # Bounded readiness retry covers connection refusal/reset during startup.
        time.sleep(0.25)
    raise RuntimeError("Container readiness timed out after 60 seconds")


def smoke(image):
    name = "aquamonitor-smoke-" + uuid.uuid4().hex
    volume = None
    containers = []
    failed = True
    try:
        volume = docker("volume", "create", "--label", "aquamonitor.smoke=true", name + "-data")
        environment = {"Jwt__Key": secrets.token_urlsafe(48)}
        container = docker(
            "create", "--name", name, "--label", "aquamonitor.smoke=true",
            "--publish", "127.0.0.1::8080", "--volume", volume + ":/app/data",
            "--env", "Jwt__Key", image, environment=environment,
        )
        containers.append(container)
        docker("start", container)
        base_url = ready(container)
        require(docker("exec", container, "printenv", "ASPNETCORE_ENVIRONMENT") == "Production",
                "Container must use Production")
        require(docker("exec", container, "id", "-u") != "0", "Container must run as non-root")
        request(base_url, "/api/WaterSensors", expected=401)

        credentials = {"email": "smoke@example.test", "password": secrets.token_urlsafe(24)}
        user = request(base_url, "/api/Auth/register", expected=201, payload={"name": "Smoke", **credentials})
        require(user["role"] == "User" and user["token"], "Registration must issue a User token")
        token = user["token"]
        sensor = request(base_url, "/api/WaterSensors", expected=201, token=token, payload={
            "name": "Smoke sensor", "location": "Isolated container", "alertThresholdLitersPerHour": 200,
        })
        sensor_id = sensor["id"]
        require(sensor_id > 0 and sensor["alertThresholdLitersPerHour"] == 200, "Invalid sensor response")
        normal = request(base_url, "/api/WaterReadings", expected=201, token=token, payload={
            "sensorId": sensor_id, "litersConsumed": 50, "flowRateLitersPerHour": 100,
        })
        require(normal["alertGenerated"] is False and normal["alertId"] is None,
                "Normal reading must not generate an alert")
        require(request(base_url, "/api/LeakAlerts", token=token)["totalCount"] == 0,
                "Normal reading persisted an unexpected alert")
        anomalous = request(base_url, "/api/WaterReadings", expected=201, token=token, payload={
            "sensorId": sensor_id, "litersConsumed": 75, "flowRateLitersPerHour": 600,
        })
        require(anomalous["alertGenerated"] is True and anomalous["alertId"] > 0,
                "Anomalous reading must generate an alert")

        def verify_records(current_token):
            sensors = request(base_url, "/api/WaterSensors", token=current_token)
            require(sensors["totalCount"] == 1 and sensors["data"][0]["id"] == sensor_id,
                    "Sensor did not persist")
            readings = request(base_url, "/api/WaterReadings", token=current_token)
            require(readings["totalCount"] == 2, "Expected both readings to persist")
            by_id = {reading["id"]: reading for reading in readings["data"]}
            require(by_id[normal["readingId"]]["isAnomaly"] is False and
                    by_id[normal["readingId"]]["litersConsumed"] == 50, "Normal reading changed")
            require(by_id[anomalous["readingId"]]["isAnomaly"] is True and
                    by_id[anomalous["readingId"]]["litersConsumed"] == 75, "Anomalous reading changed")
            alerts = request(base_url, "/api/LeakAlerts", token=current_token)
            require(alerts["totalCount"] == 1, "Expected exactly one persisted alert")
            alert = alerts["data"][0]
            require(alert["id"] == anomalous["alertId"] and alert["sensorId"] == sensor_id and
                    alert["severity"] == "Critical" and alert["triggerFlowRate"] == 600,
                    "Persisted alert does not match the anomaly")

        verify_records(token)
        docker("stop", "--time", "10", container)

        # Exercise the actual offline Production command twice against the same volume.
        admin_credentials = {"email": "operator@example.test", "password": secrets.token_urlsafe(24)}
        provisioning_environment = {
            "ConnectionStrings__DefaultConnection": "/app/data/aquamonitor.db",
            "BootstrapAdmin__Email": admin_credentials["email"],
            "BootstrapAdmin__Password": admin_credentials["password"],
        }
        provisioner = docker(
            "create", "--name", name + "-provision", "--volume", volume + ":/app/data",
            "--env", "ConnectionStrings__DefaultConnection", "--env", "BootstrapAdmin__Email",
            "--env", "BootstrapAdmin__Password", image, "--provision-admin",
            environment=provisioning_environment,
        )
        containers.append(provisioner)
        for _ in range(2):
            docker("start", "--attach", provisioner)
            require(docker("inspect", "--format", "{{.State.ExitCode}}", provisioner) == "0",
                    "Offline provisioning command failed")

        docker("start", container)
        base_url = ready(container)
        user = request(base_url, "/api/Auth/login", payload=credentials)
        require(user["role"] == "User", "User did not survive restart")
        verify_records(user["token"])
        admin = request(base_url, "/api/Auth/login", payload=admin_credentials)
        require(admin["role"] == "Admin", "Offline provisioned administrator cannot authenticate")
        require(request(base_url, "/api/LeakAlerts/stats", token=admin["token"])["totalAlerts"] == 1,
                "Administrator authorization or persisted alert statistics failed")
        failed = False
        print("PASS: Production, non-root, authentication, sensor, normal/anomalous readings, Critical alert, "
              "restart persistence, and idempotent offline administrator provisioning.")
    finally:
        cleanup_failed = False
        for container in reversed(containers):
            if failed:
                try:
                    print(docker("logs", "--tail", "80", container), file=sys.stderr)
                except (subprocess.SubprocessError, OSError):
                    print("Container diagnostic logs unavailable", file=sys.stderr)
            try:
                docker("rm", "--force", container)
            except (subprocess.SubprocessError, OSError):
                cleanup_failed = True
                print("Failed to remove isolated smoke container: " + container, file=sys.stderr)
        if volume:
            try:
                docker("volume", "rm", volume)
            except (subprocess.SubprocessError, OSError):
                cleanup_failed = True
                print("Failed to remove isolated smoke volume: " + volume, file=sys.stderr)
        if cleanup_failed and not failed:
            raise RuntimeError("Smoke verification passed but cleanup failed")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", default="aquamonitor:ci")
    try:
        smoke(parser.parse_args().image)
    except (RuntimeError, KeyError, ValueError, OSError, subprocess.SubprocessError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        sys.exit(1)
