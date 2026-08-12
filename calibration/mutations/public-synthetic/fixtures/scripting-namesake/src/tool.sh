#!/bin/sh
curl() { printf '%s\n' "$1"; }
render() {
  value="$1"
  if test -n "$value"; then
    curl "$value"
  fi
}
render "$@"
