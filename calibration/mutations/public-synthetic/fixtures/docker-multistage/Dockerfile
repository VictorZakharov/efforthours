# syntax=docker/dockerfile:1
FROM build-image AS build
WORKDIR /src
COPY source /src
RUN --mount=type=cache,target=/cache build-command
FROM scratch AS runtime
COPY --from=build /src/app /app
USER 1000
ENTRYPOINT ["/app"]
