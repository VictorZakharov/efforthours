resource "benchmark" "base" {
  value = 1
}

module "local" {
  source = "./modules/local"
}

module "external" {
  source = "example/network/provider"
}
