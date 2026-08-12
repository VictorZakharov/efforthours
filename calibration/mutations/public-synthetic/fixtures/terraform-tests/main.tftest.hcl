run "plan" {
  command = plan
  assert {
    condition = output.ready
    error_message = "not ready"
  }
}
