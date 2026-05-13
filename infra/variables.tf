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
  description = "AWS region for the provider and for tags/locals."
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

variable "container_image" {
  description = "Lambda container image URI (ECR or public base image). CI sets TF_VAR_container_image."
  type        = string
  default     = "public.ecr.aws/lambda/dotnet:8"
}

variable "use_localstack" {
  description = "When true, route the AWS provider to LocalStack (CI and local plan without real AWS keys)."
  type        = bool
  default     = false
}

variable "localstack_endpoint" {
  description = "LocalStack edge URL when use_localstack is true (e.g. http://127.0.0.1:4566)."
  type        = string
  default     = "http://127.0.0.1:4566"
}
