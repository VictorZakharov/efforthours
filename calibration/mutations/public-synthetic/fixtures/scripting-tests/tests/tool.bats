@test "render accepts a value" {
  run ./src/tool.sh value
  [ "$status" -eq 0 ]
}
