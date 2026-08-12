resource "benchmark" "base" {
  value = 1
}

variable "region" {
  type = string
  validation {
    condition = length(var.region) > 2
    error_message = "invalid region"
  }
}
