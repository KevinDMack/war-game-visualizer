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

# Get the current user's object ID
CURRENT_USER_ID=$(az ad signed-in-user show --query id -o tsv 2>/dev/null || echo "")

if [ -z "$CURRENT_USER_ID" ]; then
  # If signed-in-user doesn't work, try getting it from the account context
  CURRENT_USER_ID=$(az account show --query user.objectId -o tsv)
fi

# Assign Storage Blob Data Contributor role to the current user
echo "Assigning Storage Blob Data Contributor role to current user..."
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "$CURRENT_USER_ID" \
  --scope "$STORAGE_ACCOUNT_ID" \
  --assignee-principal-type User \
  --output none 2>/dev/null || echo "Role assignment already exists or user already has permissions."

# Note: Storage account key authentication is disabled via the Terraform configuration
# Network rules are kept permissive to allow Azure AD-authenticated requests
# since key-based auth is already disabled at the resource level

# Create container using RBAC (role-based access control)
# This uses the current Azure CLI authentication context without requiring storage account keys
echo "Creating container '$CONTAINER_NAME'..."
az storage container create \
  --name "$CONTAINER_NAME" \
  --account-name "$STORAGE_ACCOUNT_NAME" \
  --auth-mode login \
  --output none || echo "Container already exists."

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
