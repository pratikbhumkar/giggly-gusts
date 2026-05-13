output "display_name" {
  description = "Display string combining project and environment."
  value       = "${var.project_name} (${var.environment})"
}
