# Status formatter sample

This library converts a Boolean health observation into the stable text values
`ok` and `down`.

## Usage

Create a `StatusFormatter` and call `Format`. Pass `true` for a healthy service and
`false` for an unhealthy service. The library has no external runtime dependencies.
