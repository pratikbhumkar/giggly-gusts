resource "aws_iam_role" "lambda_exec" {
  name = "${module.naming.name_prefix}-lambda-exec"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      },
    ]
  })

  tags = module.naming.standard_tags
}

resource "aws_iam_role_policy_attachment" "lambda_basic_execution" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_cloudwatch_log_group" "api_lambda" {
  name              = module.naming.log_group_name
  retention_in_days = 14
  tags              = module.naming.standard_tags
}

# Phase 6 env: feature flags + retry knobs are passed in from Terraform variables.
# Names match WeatherOptions binding (Weather:UseOpenMeteo, Weather:Http:AttemptTimeoutMs, ...) using
# the .NET configuration provider's `__` separator convention.
locals {
  lambda_environment_variables = {
    ASPNETCORE_ENVIRONMENT = "Production"

    Weather__UseOpenMeteo           = tostring(var.use_open_meteo)
    Weather__MaintenanceMode        = tostring(var.maintenance_mode)
    Weather__CacheSeconds           = tostring(var.weather_cache_seconds)
    Weather__OpenMeteo__BaseUrl     = var.open_meteo_base_url
    Weather__Http__AttemptTimeoutMs = tostring(var.weather_http.attempt_timeout_ms)
    Weather__Http__MaxRetries       = tostring(var.weather_http.max_retries)
    Weather__Http__BackoffBaseMs    = tostring(var.weather_http.backoff_base_ms)
    Weather__Http__BackoffMaxMs     = tostring(var.weather_http.backoff_max_ms)
    Weather__Http__RetryOn429       = tostring(var.weather_http.retry_on_429)
  }
}

resource "aws_lambda_function" "api" {
  function_name = module.naming.lambda_function_name
  role          = aws_iam_role.lambda_exec.arn
  package_type  = "Image"
  image_uri     = var.container_image
  architectures = ["arm64"]
  timeout       = 10
  memory_size   = 256

  # publish=true cuts a new version on every image / env change so the `live` alias
  # can flip to a fresh immutable version (and provisioned concurrency can bind to it).
  publish = true

  environment {
    variables = local.lambda_environment_variables
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_basic_execution,
    aws_cloudwatch_log_group.api_lambda,
  ]

  tags = module.naming.standard_tags
}

resource "aws_lambda_alias" "live" {
  name             = var.lambda_alias_name
  description      = "Stable target for API Gateway / clients; cuts a fresh published version per release."
  function_name    = aws_lambda_function.api.function_name
  function_version = aws_lambda_function.api.version
}

resource "aws_lambda_provisioned_concurrency_config" "live" {
  count = var.provisioned_concurrency_count > 0 ? 1 : 0

  function_name                     = aws_lambda_function.api.function_name
  qualifier                         = aws_lambda_alias.live.name
  provisioned_concurrent_executions = var.provisioned_concurrency_count
}
