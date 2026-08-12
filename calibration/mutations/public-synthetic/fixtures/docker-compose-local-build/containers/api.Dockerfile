FROM build-image AS build
COPY source /source
RUN build-command
FROM scratch
COPY --from=build /source/app /app
ENTRYPOINT ["/app"]
