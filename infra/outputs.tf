output "registry_endpoint" {
  description = "Container registry endpoint (images are pushed here)."
  value       = scaleway_registry_namespace.main.endpoint
}

output "container_namespace_id" {
  description = "Serverless Containers namespace ID (the deploy pipeline creates the container here)."
  value       = scaleway_container_namespace.main.id
}

output "database_url" {
  description = "Set this as the DATABASE_URL GitHub secret (and Scaleway container secret)."
  value       = local.database_url
  sensitive   = true
}
