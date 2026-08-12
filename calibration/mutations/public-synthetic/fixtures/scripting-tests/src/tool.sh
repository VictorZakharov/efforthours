#!/bin/sh
render() {
  value="$1"
  if test -n "$value"; then
    printf '%s\n' "$value"
  fi
}
render "$@"
