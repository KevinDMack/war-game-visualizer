#!/bin/bash

set -e

# Script to delete the Azure storage account for Terraform state backend
# Usage: ./scripts/delete-terraform-backend.sh [--env {public|usgovernment}]

AZURE_ENVIRONMENT="usgovernment"
RESOURCE_GROUP_NAME="tfstate-rg"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --env)
      AZURE_ENVIRONMENT="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--env {public|usgovernment}]"
      exit 1
      ;;
  esac
done

# Validate environment
if [[ ! "$AZURE_ENVIRONMENT" =~ ^(public|usgovernment)$ ]]; then
  echo "Error: Invalid Azure environment '$AZURE_ENVIRONMENT'. Must be 'public' or 'usgovernment'."
  exit 1
fi

# Set cloud environment for Azure CLI
if [ "$AZURE_ENVIRONMENT" = "usgovernment" ]; then
  az cloud set --name AzureUSGovernment
else
  az cloud set --name AzureCloud
fi

echo "Terraform Backend Deletion - Azure $AZURE_ENVIRONMENT"
echo ""
echo "⚠️  WARNING: This will permanently delete the resource group '$RESOURCE_GROUP_NAME'"
echo "    and all Terraform state files stored in it."
echo ""
read -p "Are you sure you want to delete this resource group? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
  echo "Deletion cancelled."
  exit 0
fi

# Check if already logged in
if ! az account show > /dev/null 2>&1; then
  echo "Not logged into Azure. Please run 'az login' first."
  exit 1
fi

# Check if resource group exists
echo "Checking if resource group exists..."
if ! az group exists --name "$RESOURCE_GROUP_NAME" --output json | grep -q "true"; then
  echo "✓ Resource group '$RESOURCE_GROUP_NAME' does not exist."
  exit 0
fi

# Delete the resource group
echo ""
echo "Deleting resource group '$RESOURCE_GROUP_NAME'..."
az group delete \
  --name "$RESOURCE_GROUP_NAME" \
  --yes \
  --no-wait

# Wait for the deletion to complete
echo "Waiting for resource group deletion to complete..."
MAX_WAIT=3600  # 60 minutes max
ELAPSED=0
INTERVAL=10   # Check every 10 seconds

while [ $ELAPSED -lt $MAX_WAIT ]; do
  if ! az group exists --name "$RESOURCE_GROUP_NAME" --output json 2>/dev/null | grep -q "true"; then
    echo ""
    echo "✓ Resource group '$RESOURCE_GROUP_NAME' has been successfully deleted."
    exit 0
  fi

  ELAPSED=$((ELAPSED + INTERVAL))
  echo "  Still deleting... ($ELAPSED seconds elapsed)"
  sleep $INTERVAL
done

echo ""
echo "✗ Resource group deletion timed out after $MAX_WAIT seconds."
echo "  You can check the deletion status with:"
echo "  az group exists --name $RESOURCE_GROUP_NAME"
exit 1
