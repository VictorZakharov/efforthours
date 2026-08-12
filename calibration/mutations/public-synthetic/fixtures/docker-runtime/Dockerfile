FROM scratch
COPY app /app
ENV MODE=production
USER 1000
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK CMD ["/app", "health"]
ENTRYPOINT ["/app"]
