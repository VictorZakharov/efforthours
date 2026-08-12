resource "benchmark" "base" {
  value = 1
}

variable "api_token" {
  type = string
  sensitive = true
}
