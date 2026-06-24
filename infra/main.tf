terraform {
  required_providers {
    scaleway = {
      source  = "scaleway/scaleway"
      version = "~> 2.40"
    }
  }
}

# Credentials come from the environment:
#   SCW_ACCESS_KEY, SCW_SECRET_KEY, SCW_DEFAULT_PROJECT_ID, SCW_DEFAULT_ORGANIZATION_ID
provider "scaleway" {
  region = var.region
}

# --- Container registry (the deploy pipeline pushes the image here) ---
resource "scaleway_registry_namespace" "main" {
  name      = var.name
  region    = var.region
  is_public = false
}

# --- Serverless Containers namespace (the deploy pipeline creates the container inside it) ---
resource "scaleway_container_namespace" "main" {
  name   = var.name
  region = var.region
}

# --- Serverless SQL database ---
resource "scaleway_sdb_sql_database" "main" {
  name    = var.db_name
  region  = var.region
  min_cpu = 0 # scale to zero
  max_cpu = var.db_max_cpu
}

# --- IAM application + key used as the database login (username = app id, password = secret key) ---
resource "scaleway_iam_application" "db" {
  name = "${var.name}-db"
}

resource "scaleway_iam_policy" "db" {
  name           = "${var.name}-db-access"
  application_id = scaleway_iam_application.db.id

  rule {
    project_ids = [var.project_id]
    # Verify the exact name for your account: `scw iam permission-set list | grep -i sql`
    permission_set_names = [var.db_permission_set]
  }
}

resource "scaleway_iam_api_key" "db" {
  application_id = scaleway_iam_application.db.id
  description    = "${var.name} serverless SQL access"
}

locals {
  # endpoint looks like: postgres://<host>/<db-name>
  db_host = regex("postgres://([^/:]+)", scaleway_sdb_sql_database.main.endpoint)[0]

  # Npgsql key-value form (Npgsql does not parse the postgres:// URI).
  # Serverless SQL login = IAM principal (application) id; password = API secret key.
  # If auth fails, try Username = the API access key instead of the application id.
  database_url = join(";", [
    "Host=${local.db_host}",
    "Port=5432",
    "Database=${var.db_name}",
    "Username=${scaleway_iam_application.db.id}",
    "Password=${scaleway_iam_api_key.db.secret_key}",
    "SSL Mode=VerifyFull",
  ])
}
