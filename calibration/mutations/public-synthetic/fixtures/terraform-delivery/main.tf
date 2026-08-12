terraform {
  required_version = ">= 1.10"
  backend "local" {
    path = "state.tfstate"
  }
}

resource "benchmark" "base" {
  value = 1
}
