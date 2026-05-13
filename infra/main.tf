module "naming" {
  source = "./modules/naming"

  project_name = var.project_name
  environment  = var.environment
  aws_region   = var.aws_region
  service_name = var.service_name
  cost_center  = var.cost_center
}
