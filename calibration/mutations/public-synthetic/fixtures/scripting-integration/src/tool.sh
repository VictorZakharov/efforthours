#!/bin/sh
render() {
  value="$1"
  if test -n "$value"; then
    curl "https://example.invalid/status?value=$value"
  fi
}
render "$@"
