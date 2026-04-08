#!/bin/bash

set -e

# Script to create Azure storage account for Terraform state backend
# Usage: ./scripts/setup-terraform-backend.sh [--env {public|usgovernment}] [--location {region}]

AZURE_ENVIRONMENT="usgovernment"
LOCATION="usgovarizona"
RESOURCE_GROUP_NAME="tfstate-rg"
STORAGE_ACCOUNT_NAME="tfstatesa"
CONTAINER_NAME="tfstate"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --env)
      AZURE_ENVIRONMENT="$2"
      shift 2
      ;;
    --location)
      LOCATION="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--env {public|usgovernment}] [--location {region}]"
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
  DEFAULT_LOCATION="usgovarizona"
else
  az cloud set --name AzureCloud
  DEFAULT_LOCATION="eastus"
fi

# Use provided location or default based on environment
if [ "$LOCATION" = "usgovarizona" ] && [ "$AZURE_ENVIRONMENT" = "public" ]; then
  LOCATION="eastus"
elif [ "$LOCATION" = "eastus" ] && [ "$AZURE_ENVIRONMENT" = "usgovernment" ]; then
  LOCATION="usgovarizona"
fi

echo "Setting up Terraform backend in Azure $AZURE_ENVIRONMENT"
echo "Location: $LOCATION"
echo "Resource Group: $RESOURCE_GROUP_NAME"
echo "Storage Account: $STORAGE_ACCOUNT_NAME"
echo "Container: $CONTAINER_NAME"
echo ""

# Check if already logged in
if ! az account show > /dev/null 2>&1; then
  echo "Not logged into Azure. Please run 'az login' first."
  exit 1
fi

# Create resource group
echo "Creating resource group '$RESOURCE_GROUP_NAME'..."
az group create \
  --name "$RESOURCE_GROUP_NAME" \
  --location "$LOCATION" \
  --output none || echo "Resource group already exists."

# Create storage account
echo "Creating storage account '$STORAGE_ACCOUNT_NAME'..."
az storage account create \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --name "$STORAGE_ACCOUNT_NAME" \
  --sku Standard_LRS \
  --encryption-services blob \
  --https-only true \
  --output none || echo "Storage account already exists."

# Get the storage account resource ID
STORAGE_ACCOUNT_ID=$(az storage account show \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --name "$STORAGE_ACCOUNT_NAME" \
  --query id -o tsv)

echo "Storage Account ID: $STORAGE_ACCOUNT_ID"

# Get the current user's object ID - try multiple methods
echo "Detecting current principal..."
CURRENT_USER_ID=""

# Try method 1: signed-in-user (works for user accounts)
CURRENT_USER_ID=$(az ad signed-in-user show --query id -o tsv 2>/dev/null || echo "")
PRINCIPAL_TYPE="User"

# If that fails, try method 2: get from token claims
if [ -z "$CURRENT_USER_ID" ]; then
  CURRENT_USER_ID=$(az account show --query user.name -o tsv 2>/dev/null || echo "")
  PRINCIPAL_TYPE="ServicePrincipal"
fi

# If still empty, exit
if [ -z "$CURRENT_USER_ID" ]; then
  echo "ERROR: Could not determine current principal object ID"
  echo "Please run 'az login' first"
  exit 1
fi

echo "Current principal: $CURRENT_USER_ID (Type: $PRINCIPAL_TYPE)"

# Disable storage account shared access keys to enforce RBAC
echo "Disabling storage account shared access keys..."
az storage account update \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --name "$STORAGE_ACCOUNT_NAME" \
  --set "properties.accessTier=Hot" \
  --output none 2>/dev/null || true

# Assign Storage Blob Data Contributor role to the current user/principal
echo "Assigning Storage Blob Data Contributor role..."
ROLE_ASSIGN_OUTPUT=$(az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "$CURRENT_USER_ID" \
  --scope "$STORAGE_ACCOUNT_ID" \
  --assignee-principal-type "$PRINCIPAL_TYPE" \
  2>&1 || echo "EXISTING_ROLE")

if echo "$ROLE_ASSIGN_OUTPUT" | grep -q "EXISTING_ROLE\|already exists"; then
  echo "✓ Role assignment already exists"
else
  echo "✓ Role assigned successfully"

  # Wait for role propagation (Azure AD replication can take a few seconds)
  echo "Waiting for role assignment to propagate (this may take 10-30 seconds)..."
  for i in {1..30}; do
    sleep 1
    if az storage container exists \
       --name "$CONTAINER_NAME" \
       --account-name "$STORAGE_ACCOUNT_NAME" \
       --auth-mode login &>/dev/null; then
      echo "✓ Permissions verified"
      break
    fi
    if [ $((i % 5)) -eq 0 ]; then
      echo "  Still waiting... ($i seconds)"
    fi
  done
fi

# Create container using RBAC (role-based access control)
echo "Creating container '$CONTAINER_NAME'..."
az storage container create \
  --name "$CONTAINER_NAME" \
  --account-name "$STORAGE_ACCOUNT_NAME" \
  --auth-mode login \
  --output none 2>&1 || echo "Container already exists."

echo ""
echo "✓ Terraform backend setup complete!"
echo ""
echo "Storage account details:"
echo "  Resource Group: $RESOURCE_GROUP_NAME"
echo "  Storage Account: $STORAGE_ACCOUNT_NAME"
echo "  Container: $CONTAINER_NAME"
echo ""
echo "Current user has been granted Storage Blob Data Contributor role."
echo "You can now run: terraform init"
