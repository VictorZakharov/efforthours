#!/bin/sh
# Equivalent layout with an ordinary comment.
render()
{
  value="$1"
  if test -n "$value"
  then printf '%s\n' "$value"
  fi
}
render "$@"
