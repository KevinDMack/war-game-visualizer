data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

# ---------------------------------------------------------------------------
# User-assigned managed identity for AKS workloads
# ---------------------------------------------------------------------------
resource "azurerm_user_assigned_identity" "aks_workload" {
  name                = "aks-workload-identity"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = var.tags
}

resource "azurerm_user_assigned_identity" "aks_control_plane" {
  name                = "aks-control-plane-identity"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = var.tags
}

# ---------------------------------------------------------------------------
# Azure Container Registry
# ---------------------------------------------------------------------------
resource "azurerm_container_registry" "main" {
  name                = "wargamevisualizer${var.environment}acr"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Standard"
  admin_enabled       = false
  tags                = var.tags
}

# Grant AcrPull to the AKS workload identity
resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.aks_workload.principal_id
}

# Grant AcrPull to the AKS kubelet identity (for node-level image pulls)
resource "azurerm_role_assignment" "acr_pull_kubelet" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id
  depends_on           = [azurerm_kubernetes_cluster.main]
}

# ---------------------------------------------------------------------------
# Key Vault
# ---------------------------------------------------------------------------
resource "azurerm_key_vault" "main" {
  name                       = "wargame-${var.environment}-kv"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false

  enable_rbac_authorization = true
  tags                      = var.tags
}

# Grant Key Vault Secrets User to AKS workload identity
resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.aks_workload.principal_id
}

# Grant Key Vault Secrets Officer to the deploying principal (for initial secret creation)
resource "azurerm_role_assignment" "kv_secrets_officer_deployer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# ---------------------------------------------------------------------------
# Storage Account  (shared_access_key_enabled = false disables key access)
# ---------------------------------------------------------------------------
resource "azurerm_storage_account" "main" {
  name                     = "wargame${var.environment}sa"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = var.storage_account_replication_type

  # Disable storage account key access
  shared_access_key_enabled = false

  # Require HTTPS and minimum TLS 1.2
  enable_https_traffic_only = true
  min_tls_version           = "TLS1_2"

  blob_properties {
    versioning_enabled = true
  }

  tags = var.tags
}

resource "azurerm_storage_container" "scenarios" {
  name                  = "scenarios"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

# Grant Storage Blob Data Contributor to AKS workload identity
resource "azurerm_role_assignment" "storage_blob_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.aks_workload.principal_id
}

# ---------------------------------------------------------------------------
# Azure SQL Server and Database
# ---------------------------------------------------------------------------
resource "random_password" "sql_admin" {
  length           = 24
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "azurerm_mssql_server" "main" {
  name                         = "wargame-${var.environment}-sql"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_username
  administrator_login_password = var.sql_admin_password != "" ? var.sql_admin_password : random_password.sql_admin.result

  azuread_administrator {
    login_username              = "AKS Workload Identity"
    object_id                   = azurerm_user_assigned_identity.aks_workload.principal_id
    azuread_authentication_only = false
  }

  tags = var.tags
}

resource "azurerm_mssql_database" "main" {
  name      = var.sql_database_name
  server_id = azurerm_mssql_server.main.id
  sku_name  = "S1"
  tags      = var.tags
}

resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Store SQL connection string in Key Vault
resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "sql-connection-string"
  value        = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${var.sql_database_name};Authentication=Active Directory Managed Identity;"
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.kv_secrets_officer_deployer]
}

# Placeholder for Cesium Ion token – update value via Azure Portal or CLI after first deploy
resource "azurerm_key_vault_secret" "cesium_ion_token" {
  name         = "cesium-ion-token"
  value        = var.cesium_ion_token
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.kv_secrets_officer_deployer]
}

# ---------------------------------------------------------------------------
# AKS Cluster
# ---------------------------------------------------------------------------
resource "azurerm_kubernetes_cluster" "main" {
  name                = "wargame-${var.environment}-aks"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "wargame-${var.environment}"
  kubernetes_version  = var.aks_kubernetes_version

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.aks_control_plane.id]
  }

  default_node_pool {
    name       = "system"
    node_count = var.aks_node_count
    vm_size    = var.aks_node_vm_size

    upgrade_settings {
      max_surge = "10%"
    }
  }

  # Enable the Secret Store CSI driver add-on
  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  # Enable workload identity federation
  workload_identity_enabled = true
  oidc_issuer_enabled       = true

  network_profile {
    network_plugin    = "azure"
    load_balancer_sku = "standard"
  }

  tags = var.tags
}

# Federated identity credential – allows the Kubernetes service account to
# assume the workload identity via OIDC
resource "azurerm_federated_identity_credential" "aks_workload" {
  name                = "aks-workload-federated-credential"
  resource_group_name = azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.aks_workload.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.main.oidc_issuer_url
  subject             = "system:serviceaccount:war-game-visualizer:workload-sa"
}

# Grant the control-plane identity permission to manage network resources
resource "azurerm_role_assignment" "aks_network_contributor" {
  scope                = azurerm_resource_group.main.id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_user_assigned_identity.aks_control_plane.principal_id
}
