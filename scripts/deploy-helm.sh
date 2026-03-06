#!/usr/bin/env bash
# deploy-helm.sh
# Deploys (or upgrades) the war-game-visualizer Helm chart to an AKS cluster.
#
# Usage:
#   ./scripts/deploy-helm.sh [--env <environment>] [--values <extra-values-file>] [--dry-run]
#
# Prerequisites:
#   - kubectl configured to target the correct AKS cluster
#     (run: az aks get-credentials --resource-group <rg> --name <aks-name>)
#   - Helm 3 installed
#   - ACR_LOGIN_SERVER, KEY_VAULT_NAME, KEY_VAULT_TENANT_ID, WORKLOAD_IDENTITY_CLIENT_ID set

set -euo pipefail

# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------
log() { echo "[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"; }
err() { log "ERROR: $*" >&2; exit 1; }

# --------------------------------------------------------------------------
# Defaults
# --------------------------------------------------------------------------
ENVIRONMENT="${ENVIRONMENT:-dev}"
NAMESPACE="war-game-visualizer"
RELEASE_NAME="war-game-visualizer"
IMAGE_TAG="${IMAGE_TAG:-latest}"
DRY_RUN=""
EXTRA_VALUES_FILE=""

# --------------------------------------------------------------------------
# Argument parsing
# --------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)         ENVIRONMENT="$2"; shift 2 ;;
    --values)      EXTRA_VALUES_FILE="$2"; shift 2 ;;
    --dry-run)     DRY_RUN="--dry-run"; shift ;;
    *) err "Unknown argument: $1" ;;
  esac
done

# --------------------------------------------------------------------------
# Resolve paths
# --------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
HELM_DIR="${REPO_ROOT}/helm"

# --------------------------------------------------------------------------
# Validate required env vars
# --------------------------------------------------------------------------
: "${ACR_LOGIN_SERVER:?ACR_LOGIN_SERVER must be set}"
: "${KEY_VAULT_NAME:?KEY_VAULT_NAME must be set}"
: "${KEY_VAULT_TENANT_ID:?KEY_VAULT_TENANT_ID must be set}"
: "${WORKLOAD_IDENTITY_CLIENT_ID:?WORKLOAD_IDENTITY_CLIENT_ID must be set}"

# --------------------------------------------------------------------------
# Create namespace if needed
# --------------------------------------------------------------------------
log "Ensuring namespace '${NAMESPACE}' exists..."
kubectl get namespace "${NAMESPACE}" > /dev/null 2>&1 || \
  kubectl create namespace "${NAMESPACE}"

# --------------------------------------------------------------------------
# Add / update Helm repositories
# --------------------------------------------------------------------------
log "Adding Dapr Helm repository..."
helm repo add dapr https://dapr.github.io/helm-charts/ --force-update
helm repo update

# --------------------------------------------------------------------------
# Build Helm dependencies (Dapr sub-chart)
# --------------------------------------------------------------------------
log "Building Helm chart dependencies..."
helm dependency build "${HELM_DIR}"

# --------------------------------------------------------------------------
# Helm upgrade / install
# --------------------------------------------------------------------------
EXTRA_VALUES_ARG=""
[[ -n "${EXTRA_VALUES_FILE}" ]] && EXTRA_VALUES_ARG="--values ${EXTRA_VALUES_FILE}"

log "Deploying Helm release '${RELEASE_NAME}' to namespace '${NAMESPACE}'..."
# shellcheck disable=SC2086
helm upgrade --install "${RELEASE_NAME}" "${HELM_DIR}" \
  --namespace "${NAMESPACE}" \
  --create-namespace \
  --set global.environment="${ENVIRONMENT}" \
  --set global.acrLoginServer="${ACR_LOGIN_SERVER}" \
  --set global.imageTag="${IMAGE_TAG}" \
  --set workloadIdentity.clientId="${WORKLOAD_IDENTITY_CLIENT_ID}" \
  --set keyVault.name="${KEY_VAULT_NAME}" \
  --set keyVault.tenantId="${KEY_VAULT_TENANT_ID}" \
  --atomic \
  --timeout 10m \
  ${EXTRA_VALUES_ARG} \
  ${DRY_RUN}

log "Deployment complete."
