output "name_prefix" {
  description = "Stem used for future AWS resource names."
  value       = local.name_prefix
}

output "standard_tags" {
  description = "Tag map reserved for future aws provider default_tags (Phase 4+)."
  value       = local.standard_tags
}

output "lambda_function_name" {
  description = "Planned Lambda function name (not created in Phase 3)."
  value       = local.lambda_function_name
}

output "log_group_name" {
  description = "Planned CloudWatch log group path for the API Lambda."
  value       = local.log_group_name
}

output "ecr_repository_name" {
  description = "Planned ECR repository id for container images."
  value       = local.ecr_repository_name
}

output "api_gateway_stage_name" {
  description = "Planned API Gateway stage / label helper."
  value       = local.api_gateway_stage_name
}
