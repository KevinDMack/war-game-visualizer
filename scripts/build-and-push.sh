#!/usr/bin/env bash
# build-and-push.sh
# Builds a container image for a given service and pushes it to the ACR.
#
# Usage:
#   ./scripts/build-and-push.sh <service-name> [image-tag]
#
# Example:
#   ./scripts/build-and-push.sh web-app latest
#   ./scripts/build-and-push.sh scenario-service v1.2.3
#
# Prerequisites:
#   - Azure CLI logged in (az login or service principal)
#   - Docker daemon running
#   - ACR_NAME environment variable set, OR pass --acr-name <name>

set -euo pipefail

# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------
usage() {
  echo "Usage: $0 <service-name> [image-tag]"
  echo ""
  echo "Environment variables:"
  echo "  ACR_NAME          (required) Azure Container Registry name (without .azurecr.io)"
  echo "  RESOURCE_GROUP    (optional) Resource group for az acr login"
  echo ""
  exit 1
}

log() { echo "[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"; }
err() { log "ERROR: $*" >&2; exit 1; }

# --------------------------------------------------------------------------
# Argument parsing
# --------------------------------------------------------------------------
SERVICE_NAME="${1:-}"
IMAGE_TAG="${2:-latest}"

[[ -z "${SERVICE_NAME}" ]] && usage

# --------------------------------------------------------------------------
# Resolve paths
# --------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SERVICE_DIR="${REPO_ROOT}/services/${SERVICE_NAME}"
BASE_DIR="${REPO_ROOT}/services/base"

[[ -d "${SERVICE_DIR}" ]] || err "Service directory not found: ${SERVICE_DIR}"
[[ -f "${SERVICE_DIR}/Dockerfile" ]] || err "Dockerfile not found in: ${SERVICE_DIR}"

# --------------------------------------------------------------------------
# Resolve ACR
# --------------------------------------------------------------------------
ACR_NAME="${ACR_NAME:-}"
[[ -z "${ACR_NAME}" ]] && err "ACR_NAME environment variable is not set."

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
IMAGE_NAME="${ACR_LOGIN_SERVER}/${SERVICE_NAME}:${IMAGE_TAG}"

# --------------------------------------------------------------------------
# Build
# --------------------------------------------------------------------------
log "Logging in to ACR: ${ACR_LOGIN_SERVER}"
az acr login --name "${ACR_NAME}"

log "Building image: ${IMAGE_NAME}"
docker build \
  --file "${SERVICE_DIR}/Dockerfile" \
  --tag "${IMAGE_NAME}" \
  --build-arg BASE_IMAGE="${ACR_LOGIN_SERVER}/base:latest" \
  "${REPO_ROOT}/services"

log "Pushing image: ${IMAGE_NAME}"
docker push "${IMAGE_NAME}"

log "Done. Image pushed: ${IMAGE_NAME}"
