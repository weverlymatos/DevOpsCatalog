resource "kubernetes_namespace_v1" "terraform_lab" {
  metadata {
    name = var.namespace
  }
}

resource "kubernetes_config_map_v1" "terraform_config" {
  metadata {
    name      = "terraform-demo"
    namespace = kubernetes_namespace_v1.terraform_lab.metadata[0].name
  }

  data = {
    managed_by  = "terraform"
    environment = var.environment
    project     = var.project_name
  }
}