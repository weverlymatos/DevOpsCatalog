variable "namespace" {
  description = "Namespace Kubernetes utilizado pelo laboratório Terraform"
  type        = string
  default     = "terraform-lab"
}

variable "environment" {
  description = "Ambiente da aplicação"
  type        = string
  default     = "development"
}

variable "project_name" {
  description = "Nome do projeto"
  type        = string
  default     = "DevOpsCatalog"
}