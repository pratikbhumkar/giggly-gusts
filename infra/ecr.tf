resource "aws_ecr_repository" "api" {
  name = module.naming.ecr_repository_name

  # MUTABLE so phase-5 CI can re-tag :latest / :<sha> while iterating.
  # Switch to IMMUTABLE before any real deployment so production
  # `image_uri` references resolve to a single immutable digest.
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }

  tags = module.naming.standard_tags
}

# Keep ECR storage costs predictable: drop untagged images after 14 days and
# cap the number of retained tagged images. Real deployments should pin
# image_uri to a digest (see README) so this lifecycle never deletes prod refs.
resource "aws_ecr_lifecycle_policy" "api" {
  repository = aws_ecr_repository.api.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Expire untagged images after 14 days"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = 14
        }
        action = {
          type = "expire"
        }
      },
      {
        rulePriority = 2
        description  = "Keep last 20 tagged images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 20
        }
        action = {
          type = "expire"
        }
      },
    ]
  })
}
