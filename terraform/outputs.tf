output "namespace_name" {
  description = "Namespace criado pelo Terraform"
  value       = kubernetes_namespace_v1.terraform_lab.metadata[0].name
}

output "config_map_name" {
  description = "ConfigMap criado pelo Terraform"
  value       = kubernetes_config_map_v1.terraform_config.metadata[0].name
}