variable "project_name" {
  type        = string
  description = "Project key used in names and tags."
}

variable "environment" {
  type        = string
  description = "Environment segment for names and tags."
}

variable "aws_region" {
  type        = string
  description = "Planned deployment region (naming and tag context only until the AWS provider is added)."
}

variable "service_name" {
  type        = string
  description = "Service label for tags and future resource names."
}

variable "cost_center" {
  type        = string
  description = "Optional cost center for standard_tags (may be empty)."
}
