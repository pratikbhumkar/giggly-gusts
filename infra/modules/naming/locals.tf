locals {
  name_prefix = "${var.project_name}-${var.environment}"

  standard_tags = {
    Project     = var.project_name
    Environment = var.environment
    Service     = var.service_name
    CostCenter  = var.cost_center
    ManagedBy   = "terraform"
    Region      = var.aws_region
  }

  # Planned resource names (strings only; no AWS provider in Phase 3).
  lambda_function_name   = "${local.name_prefix}-api"
  log_group_name         = "/aws/lambda/${local.lambda_function_name}"
  ecr_repository_name    = "${local.name_prefix}-api"
  api_gateway_stage_name = "${local.name_prefix}-http"
}
