"""
War Game Visualizer – Web Application
A Flask application that serves the Cesium-based globe UI and proxies
scenario CRUD operations to the scenario-service via Dapr.
"""

import json
import os
import uuid
from dataclasses import asdict, dataclass, field
from typing import List, Optional

import requests
from flask import Flask, jsonify, render_template, request

app = Flask(__name__)

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
DAPR_HTTP_PORT = int(os.getenv("DAPR_HTTP_PORT", "3500"))
SCENARIO_SERVICE_APP_ID = os.getenv("SCENARIO_SERVICE_APP_ID", "scenario-service")
CESIUM_ION_TOKEN = os.getenv("CESIUM_ION_TOKEN", "")

DAPR_BASE_URL = f"http://localhost:{DAPR_HTTP_PORT}/v1.0/invoke/{SCENARIO_SERVICE_APP_ID}/method"


# ---------------------------------------------------------------------------
# In-memory store (used when running locally without the scenario-service)
# ---------------------------------------------------------------------------
_SCENARIOS: List[dict] = []


def _dapr_available() -> bool:
    """Return True when Dapr sidecar is reachable."""
    try:
        resp = requests.get(f"http://localhost:{DAPR_HTTP_PORT}/v1.0/healthz", timeout=1)
        return resp.status_code == 204
    except requests.RequestException:
        return False


# ---------------------------------------------------------------------------
# Helper: call scenario-service via Dapr or fall back to local store
# ---------------------------------------------------------------------------
def _get_scenarios() -> List[dict]:
    if _dapr_available():
        resp = requests.get(f"{DAPR_BASE_URL}/scenarios", timeout=5)
        resp.raise_for_status()
        return resp.json().get("scenarios", [])
    return _SCENARIOS


def _get_scenario(scenario_id: str) -> Optional[dict]:
    if _dapr_available():
        resp = requests.get(f"{DAPR_BASE_URL}/scenarios/{scenario_id}", timeout=5)
        if resp.status_code == 404:
            return None
        resp.raise_for_status()
        return resp.json().get("scenario")
    return next((s for s in _SCENARIOS if s["scenarioId"] == scenario_id), None)


def _create_scenario(data: dict) -> dict:
    data.setdefault("scenarioId", str(uuid.uuid4()))
    if _dapr_available():
        resp = requests.post(f"{DAPR_BASE_URL}/scenarios", json=data, timeout=5)
        resp.raise_for_status()
        return resp.json().get("scenario", data)
    _SCENARIOS.append(data)
    return data


def _update_scenario(scenario_id: str, data: dict) -> Optional[dict]:
    data["scenarioId"] = scenario_id
    if _dapr_available():
        resp = requests.put(f"{DAPR_BASE_URL}/scenarios/{scenario_id}", json=data, timeout=5)
        if resp.status_code == 404:
            return None
        resp.raise_for_status()
        return resp.json().get("scenario", data)
    for i, s in enumerate(_SCENARIOS):
        if s["scenarioId"] == scenario_id:
            _SCENARIOS[i] = data
            return data
    return None


def _delete_scenario(scenario_id: str) -> bool:
    if _dapr_available():
        resp = requests.delete(f"{DAPR_BASE_URL}/scenarios/{scenario_id}", timeout=5)
        if resp.status_code == 404:
            return False
        resp.raise_for_status()
        return True
    global _SCENARIOS
    before = len(_SCENARIOS)
    _SCENARIOS = [s for s in _SCENARIOS if s["scenarioId"] != scenario_id]
    return len(_SCENARIOS) < before


# ---------------------------------------------------------------------------
# Routes – UI
# ---------------------------------------------------------------------------
@app.route("/")
def index():
    return render_template("index.html", cesium_ion_token=CESIUM_ION_TOKEN)


# ---------------------------------------------------------------------------
# Routes – REST API (consumed by the front-end via fetch)
# ---------------------------------------------------------------------------
@app.route("/api/scenarios", methods=["GET"])
def list_scenarios():
    return jsonify({"scenarios": _get_scenarios()})


@app.route("/api/scenarios/<scenario_id>", methods=["GET"])
def get_scenario(scenario_id: str):
    scenario = _get_scenario(scenario_id)
    if scenario is None:
        return jsonify({"error": "Not found"}), 404
    return jsonify({"scenario": scenario})


@app.route("/api/scenarios", methods=["POST"])
def create_scenario():
    data = request.get_json(force=True)
    scenario = _create_scenario(data)
    return jsonify({"scenario": scenario}), 201


@app.route("/api/scenarios/<scenario_id>", methods=["PUT"])
def update_scenario(scenario_id: str):
    data = request.get_json(force=True)
    scenario = _update_scenario(scenario_id, data)
    if scenario is None:
        return jsonify({"error": "Not found"}), 404
    return jsonify({"scenario": scenario})


@app.route("/api/scenarios/<scenario_id>", methods=["DELETE"])
def delete_scenario(scenario_id: str):
    success = _delete_scenario(scenario_id)
    if not success:
        return jsonify({"error": "Not found"}), 404
    return jsonify({"success": True})


# ---------------------------------------------------------------------------
# Health probe (used by Kubernetes liveness / readiness probes)
# ---------------------------------------------------------------------------
@app.route("/healthz")
def healthz():
    return jsonify({"status": "ok"})


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=os.getenv("FLASK_DEBUG", "false").lower() == "true")
