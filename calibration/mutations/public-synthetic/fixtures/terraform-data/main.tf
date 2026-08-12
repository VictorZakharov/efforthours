resource "benchmark" "base" {
  value = 1
}

data "lookup" "current" {
  key = "current"
}
