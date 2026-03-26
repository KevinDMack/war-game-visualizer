#!/usr/bin/env bash
# build-service.sh
# Runs unit tests (if present) and builds the Docker image for one or all services.
#
# Usage:
#   ./services/web-app/build-service.sh [options]
#
# Options:
#   -s, --service <name>   Build a specific service by subdirectory name (default: web-app)
#       --all-services     Build ALL services under services/ ('base' is always built first)
#   -t, --tag <tag>        Docker image tag (default: latest)
#   -h, --help             Show this help message
#
# Prerequisites:
#   - .NET 8 SDK (dotnet CLI) — required only for services that include a test project
#   - Docker daemon running
#   - BASE_IMAGE environment variable (optional) — full base image reference, e.g.
#       BASE_IMAGE="myacr.azurecr.io/base:latest"
#     If not set, the base image is built locally first.
#
# Examples:
#   # Build just the web-app service (default)
#   ./services/web-app/build-service.sh
#
#   # Build a specific service with a custom tag
#   ./services/web-app/build-service.sh --service web-app --tag v1.2.0
#
#   # Build all services, tagging each as 'latest'
#   ./services/web-app/build-service.sh --all-services
#
#   # Build all services using a pre-built base image from ACR
#   BASE_IMAGE="myacr.azurecr.io/base:latest" ./services/web-app/build-service.sh --all-services

set -euo pipefail

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
log()  { echo "[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"; }
err()  { log "ERROR: $*" >&2; exit 1; }
step() { echo; echo "======================================================"; echo "  $*"; echo "======================================================"; }

usage() {
  grep '^#' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

# ---------------------------------------------------------------------------
# Resolve repo root from the location of this script
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SERVICES_ROOT="${REPO_ROOT}/services"

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
SERVICE_NAME="web-app"   # default to the service that owns this script
IMAGE_TAG="latest"
ALL_SERVICES=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    -s|--service)
      [[ $# -ge 2 ]] || err "--service requires an argument"
      SERVICE_NAME="$2"
      shift 2
      ;;
    --all-services)
      ALL_SERVICES=true
      shift
      ;;
    -t|--tag)
      [[ $# -ge 2 ]] || err "--tag requires an argument"
      IMAGE_TAG="$2"
      shift 2
      ;;
    -h|--help)
      usage
      ;;
    *)
      err "Unknown argument: $1  (use --help for usage)"
      ;;
  esac
done

# ---------------------------------------------------------------------------
# Core: build a single service
# ---------------------------------------------------------------------------
build_service() {
  local service="$1"
  local tag="$2"
  local base_image="$3"   # pre-resolved base image tag for this run
  local service_dir="${SERVICES_ROOT}/${service}"

  [[ -d "${service_dir}" ]]        || err "Service directory not found: ${service_dir}"
  [[ -f "${service_dir}/Dockerfile" ]] || err "No Dockerfile in: ${service_dir}"

  step "Building service: ${service} (tag: ${tag})"

  # ---- Run tests if a *.Tests.csproj is present --------------------------
  local test_proj
  test_proj="$(find "${service_dir}" -maxdepth 2 -name "*.Tests.csproj" | head -1)"

  if [[ -n "${test_proj}" ]]; then
    log "Found test project: ${test_proj}"
    dotnet test "${test_proj}" \
      --configuration Release \
      --logger "console;verbosity=normal"
    log "All tests passed."
  else
    log "No test project found — skipping tests."
  fi

  # ---- Build Docker image ------------------------------------------------
  local image_name="${service}:${tag}"
  log "Building Docker image: ${image_name}"

  # Use the repository root as the build context so that cross-project
  # COPY instructions (e.g. referencing libraries/) work for all services.
  docker build \
    --file "${service_dir}/Dockerfile" \
    --tag "${image_name}" \
    --build-arg BASE_IMAGE="${base_image}" \
    "${REPO_ROOT}"

  log "Image built successfully: ${image_name}"
}

# ---------------------------------------------------------------------------
# Resolve / build the base image
# ---------------------------------------------------------------------------
resolve_base_image() {
  local tag="$1"
  local base_img="${BASE_IMAGE:-}"

  if [[ -z "${base_img}" ]]; then
    base_img="base:${tag}"
    log "BASE_IMAGE not set — building base image locally as '${base_img}'"
    docker build \
      --file "${SERVICES_ROOT}/base/Dockerfile" \
      --tag "${base_img}" \
      "${SERVICES_ROOT}/base"
    log "Base image built: ${base_img}"
  else
    log "Using base image: ${base_img}"
  fi

  echo "${base_img}"
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
BASE_IMAGE="${BASE_IMAGE:-}"

if [[ "${ALL_SERVICES}" == true ]]; then
  step "Building ALL services (base first)"

  # Resolve or build the base image (uses BASE_IMAGE env var if set, otherwise builds locally)
  RESOLVED_BASE="$(resolve_base_image "${IMAGE_TAG}")"

  # Collect all service dirs, exclude 'base' (already handled above)
  mapfile -t service_dirs < <(
    find "${SERVICES_ROOT}" -mindepth 1 -maxdepth 1 -type d \
      ! -name "base" \
      -printf "%f\n" | sort
  )

  for svc in "${service_dirs[@]}"; do
    if [[ -f "${SERVICES_ROOT}/${svc}/Dockerfile" ]]; then
      build_service "${svc}" "${IMAGE_TAG}" "${RESOLVED_BASE}"
    else
      log "Skipping '${svc}' — no Dockerfile found."
    fi
  done

  step "All services built successfully"
else
  # Single-service mode
  if [[ "${SERVICE_NAME}" == "base" ]]; then
    # Build the base image explicitly, regardless of BASE_IMAGE env var
    step "Building service: base (tag: ${IMAGE_TAG})"
    docker build \
      --file "${SERVICES_ROOT}/base/Dockerfile" \
      --tag "base:${IMAGE_TAG}" \
      "${SERVICES_ROOT}/base"
    log "Image built successfully: base:${IMAGE_TAG}"
  else
    # Resolve (or build) the base image first, then build the target service
    RESOLVED_BASE="$(resolve_base_image "${IMAGE_TAG}")"
    build_service "${SERVICE_NAME}" "${IMAGE_TAG}" "${RESOLVED_BASE}"
  fi
fi

log "Done."
