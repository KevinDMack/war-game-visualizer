#!/usr/bin/env bash
# build-base.sh
# Builds the base container image and pushes it to the ACR.
#
# Usage:
#   ./scripts/build-base.sh [image-tag]
#
# Example:
#   ./scripts/build-base.sh latest
#   ./scripts/build-base.sh v1.2.3
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
  echo "Usage: $0 [image-tag]"
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
IMAGE_TAG="${1:-latest}"

# --------------------------------------------------------------------------
# Resolve paths
# --------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
BASE_DIR="${REPO_ROOT}/services/base"

[[ -d "${BASE_DIR}" ]] || err "Base service directory not found: ${BASE_DIR}"
[[ -f "${BASE_DIR}/Dockerfile" ]] || err "Dockerfile not found in: ${BASE_DIR}"

# --------------------------------------------------------------------------
# Resolve ACR
# --------------------------------------------------------------------------
ACR_NAME="${ACR_NAME:-}"
[[ -z "${ACR_NAME}" ]] && err "ACR_NAME environment variable is not set."

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
IMAGE_NAME="${ACR_LOGIN_SERVER}/base:${IMAGE_TAG}"

# --------------------------------------------------------------------------
# Build
# --------------------------------------------------------------------------
log "Logging in to ACR: ${ACR_LOGIN_SERVER}"
az acr login --name "${ACR_NAME}"

log "Building image: ${IMAGE_NAME}"
docker build \
  --file "${BASE_DIR}/Dockerfile" \
  --tag "${IMAGE_NAME}" \
  "${BASE_DIR}"

log "Pushing image: ${IMAGE_NAME}"
docker push "${IMAGE_NAME}"

log "Done. Image pushed: ${IMAGE_NAME}"
