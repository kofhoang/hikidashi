variable "project_id" {
  type        = string
  description = "Scaleway project ID the resources live in."
}

variable "region" {
  type    = string
  default = "fr-par"
}

variable "name" {
  type        = string
  default     = "hikidashi"
  description = "Base name for the registry namespace, container namespace, and IAM application."
}

variable "db_name" {
  type    = string
  default = "hikidashi"
}

variable "db_max_cpu" {
  type    = number
  default = 8
}

variable "db_permission_set" {
  type        = string
  default     = "ServerlessSQLDatabaseFullAccess"
  description = "IAM permission set granting DB access. Verify: `scw iam permission-set list | grep -i sql`."
}
