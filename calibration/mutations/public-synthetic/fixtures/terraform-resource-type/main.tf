resource "benchmark" "base" {
  value = 1
}

resource "network" "primary" {
  cidr = "10.0.0.0/16"
}
