output "display_name" {
  description = "Display string combining project and environment."
  value       = "${var.project_name} (${var.environment})"
}

output "name_prefix" {
  description = "Naming stem from the naming module."
  value       = module.naming.name_prefix
}

output "standard_tags" {
  description = "Standard tag map for future AWS resources."
  value       = module.naming.standard_tags
}

output "lambda_function_name" {
  description = "Planned Lambda function name."
  value       = module.naming.lambda_function_name
}

output "log_group_name" {
  description = "Planned log group name."
  value       = module.naming.log_group_name
}

output "ecr_repository_name" {
  description = "Planned ECR repository name."
  value       = module.naming.ecr_repository_name
}

output "api_gateway_stage_name" {
  description = "Planned API Gateway stage name helper."
  value       = module.naming.api_gateway_stage_name
}

output "project_name" {
  description = "Echo of var.project_name for operator debugging."
  value       = var.project_name
}

output "environment" {
  description = "Echo of var.environment for operator debugging."
  value       = var.environment
}

output "aws_region" {
  description = "Echo of var.aws_region (naming context until provider wiring)."
  value       = var.aws_region
}

output "lambda_function_arn" {
  description = "Managed Lambda function ARN (plan/apply target for Phase 4+)."
  value       = aws_lambda_function.api.arn
}

output "lambda_exec_role_arn" {
  description = "IAM role ARN used by the API Lambda."
  value       = aws_iam_role.lambda_exec.arn
}

output "ecr_repository_url" {
  description = "ECR repository URL for the API Lambda container image."
  value       = aws_ecr_repository.api.repository_url
}

output "ecr_repository_arn" {
  description = "ECR repository ARN (useful for IAM policy scoping in later phases)."
  value       = aws_ecr_repository.api.arn
}

output "lambda_alias_arn" {
  description = "ARN of the stable Lambda alias (API Gateway should integrate against this)."
  value       = aws_lambda_alias.live.arn
}

output "lambda_alias_name" {
  description = "Name of the stable Lambda alias (matches var.lambda_alias_name; the runbook references it by name when invoking via the AWS CLI)."
  value       = aws_lambda_alias.live.name
}

output "lambda_function_version" {
  description = "Published version of the Lambda function targeted by the alias."
  value       = aws_lambda_function.api.version
}

# Phase 7 placeholder: the runbook references `api_base_url` but no public HTTP fronting
# (API Gateway / CloudFront / Lambda Function URL) is wired yet — those land in a later
# slice. Emitting null here keeps `terraform output api_base_url` working and signals that
# the maintainer must substitute the actual URL until the fronting slice lands.
output "api_base_url" {
  description = "Public base URL for /health and /weather. Null until the API fronting slice (API Gateway HTTP API or Function URL) lands; see README 'Deployment story (plan-only)'."
  value       = null
}
