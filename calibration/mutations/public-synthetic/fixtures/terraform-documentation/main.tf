resource "benchmark" "base" {
  value = 1
}

variable "region" {
  description = "Deployment region"
  type = string
}
