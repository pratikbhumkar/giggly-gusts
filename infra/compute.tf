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

resource "aws_lambda_function" "api" {
  function_name = module.naming.lambda_function_name
  role          = aws_iam_role.lambda_exec.arn
  package_type  = "Image"
  image_uri     = var.container_image
  architectures = ["arm64"]
  timeout       = 10
  memory_size   = 256

  depends_on = [
    aws_iam_role_policy_attachment.lambda_basic_execution,
    aws_cloudwatch_log_group.api_lambda,
  ]

  tags = module.naming.standard_tags
}
