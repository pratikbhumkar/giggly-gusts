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

variable "aws_region" {
  description = "Target AWS region for future provider wiring; used in locals/tags for Phase 3 naming only (no provider yet)."
  type        = string
  default     = "ap-southeast-2"
}

variable "service_name" {
  description = "Primary service identifier for tagging and future compute resources."
  type        = string
  default     = "giggly-gusts-api"
}

variable "cost_center" {
  description = "Optional cost allocation tag (empty string allowed)."
  type        = string
  default     = ""
}
