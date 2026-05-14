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

variable "lambda_alias_name" {
  description = "Stable Lambda alias that API Gateway / clients should target (publishes a version on every change)."
  type        = string
  default     = "live"
}

variable "provisioned_concurrency_count" {
  description = "Provisioned concurrency on the alias. 0 disables. Real environments typically use 3 (see docs/ARCHITECTURE.md §4.2)."
  type        = number
  default     = 0
  validation {
    condition     = var.provisioned_concurrency_count >= 0
    error_message = "provisioned_concurrency_count must be >= 0."
  }
}

variable "use_open_meteo" {
  description = "Phase 6 feature flag passed to the Lambda environment: enables the live Open-Meteo path with fallback."
  type        = bool
  default     = false
}

variable "maintenance_mode" {
  description = "Phase 6 feature flag passed to the Lambda environment: when true, the API short-circuits weather routes to 503."
  type        = bool
  default     = false
}

variable "weather_http" {
  description = "Per-attempt timeout, retry count, and backoff bounds for the Open-Meteo client. Mirrors WeatherOptions.Http in the app."
  type = object({
    attempt_timeout_ms = number
    max_retries        = number
    backoff_base_ms    = number
    backoff_max_ms     = number
    retry_on_429       = bool
  })
  default = {
    attempt_timeout_ms = 1500
    max_retries        = 2
    backoff_base_ms    = 100
    backoff_max_ms     = 1000
    retry_on_429       = false
  }
}

variable "weather_cache_seconds" {
  description = "Phase 8 cache TTL (seconds) for successful live lookups. 0 disables. Default 120 matches Cache-Control: max-age=120."
  type        = number
  default     = 120
  validation {
    condition     = var.weather_cache_seconds >= 0
    error_message = "weather_cache_seconds must be >= 0."
  }
}

variable "open_meteo_base_url" {
  description = "Base URL for the Open-Meteo client (override only for staging / offline test mocks)."
  type        = string
  default     = "https://api.open-meteo.com"
}
