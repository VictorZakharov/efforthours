#!/bin/sh
set -euo pipefail
render() {
  value="$1"
  if test -z "$value"; then return 2; fi
  printf '%s\n' "$value"
}
trap 'exit 1' INT TERM
render "$@"
