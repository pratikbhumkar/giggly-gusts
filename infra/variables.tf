variable "project_name" {
  description = "Short project identifier for labels and future resource names."
  type        = string
  default     = "giggly-gusts"
}

variable "environment" {
  description = "Logical environment (for example dev, staging, prod)."
  type        = string
  default     = "dev"
}
