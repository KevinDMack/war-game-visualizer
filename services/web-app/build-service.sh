#!/usr/bin/env bash
# build-service.sh
# Runs the WebApp.Tests unit tests and then builds the web-app Docker image
# using the shared .NET base image.
#
# Usage:
#   ./services/web-app/build-service.sh [image-tag]
#
# Arguments:
#   image-tag   Optional Docker image tag (default: latest)
#
# Prerequisites:
#   - .NET 8 SDK installed (dotnet CLI available)
#   - Docker daemon running
#   - BASE_IMAGE environment variable set to the full base image reference,
#     OR pass --base-image <ref> as an argument.
#     Example: BASE_IMAGE="wargamevisualizerdevacr.azurecr.io/base:latest"
#     If not set, the script builds the base image locally from services/base/Dockerfile.
#
# Examples:
#   # Build with a tag, using the base image from ACR
#   BASE_IMAGE="myacr.azurecr.io/base:latest" ./services/web-app/build-service.sh v1.2.0
#
#   # Build and tag as 'latest', building the base image locally if not set
#   ./services/web-app/build-service.sh

set -euo pipefail

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
log()  { echo "[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"; }
err()  { log "ERROR: $*" >&2; exit 1; }
step() { echo; echo "======================================================"; echo "  $*"; echo "======================================================"; }

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SERVICE_DIR="${SCRIPT_DIR}"
TEST_DIR="${SERVICE_DIR}/WebApp.Tests"
BASE_DOCKERFILE="${REPO_ROOT}/services/base/Dockerfile"

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
IMAGE_TAG="${1:-latest}"
IMAGE_NAME="web-app:${IMAGE_TAG}"
BASE_IMAGE="${BASE_IMAGE:-}"

# ---------------------------------------------------------------------------
# Step 1 – Run unit tests
# ---------------------------------------------------------------------------
step "Running unit tests (WebApp.Tests)"

[[ -f "${TEST_DIR}/WebApp.Tests.csproj" ]] \
  || err "Test project not found at ${TEST_DIR}/WebApp.Tests.csproj"

dotnet test "${TEST_DIR}/WebApp.Tests.csproj" \
  --configuration Release \
  --logger "console;verbosity=normal"

log "All tests passed."

# ---------------------------------------------------------------------------
# Step 2 – Ensure base image is available
# ---------------------------------------------------------------------------
step "Preparing base image"

if [[ -z "${BASE_IMAGE}" ]]; then
  BASE_IMAGE="war-game-visualizer/base:latest"
  log "BASE_IMAGE not set — building base image locally as '${BASE_IMAGE}'"
  docker build \
    --file "${BASE_DOCKERFILE}" \
    --tag "${BASE_IMAGE}" \
    "${REPO_ROOT}/services/base"
else
  log "Using base image: ${BASE_IMAGE}"
fi

# ---------------------------------------------------------------------------
# Step 3 – Build the web-app Docker image
# ---------------------------------------------------------------------------
step "Building Docker image: ${IMAGE_NAME}"

docker build \
  --file "${SERVICE_DIR}/Dockerfile" \
  --tag "${IMAGE_NAME}" \
  --build-arg BASE_IMAGE="${BASE_IMAGE}" \
  "${SERVICE_DIR}"

log "Docker image built successfully: ${IMAGE_NAME}"
echo
log "Done. Run with:"
log "  docker run --rm -p 5000:5000 -e CESIUM_ION_TOKEN=<token> ${IMAGE_NAME}"
