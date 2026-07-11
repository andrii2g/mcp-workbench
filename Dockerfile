FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
RUN apk add --no-cache clang build-base zlib-dev
COPY . .
RUN dotnet restore src/McpWorkbench/McpWorkbench.csproj --locked-mode -r linux-musl-x64 && \
    dotnet publish src/McpWorkbench/McpWorkbench.csproj -c Release -r linux-musl-x64 \
      --self-contained true --no-restore -p:PublishAot=true -o /out

FROM alpine:3.22
RUN apk add --no-cache libstdc++ zlib && \
    addgroup -S workbench && adduser -S -G workbench -u 1654 workbench
WORKDIR /app
COPY --from=build --chown=workbench:workbench /out/ ./
RUN mkdir /app/data && chown workbench:workbench /app/data
USER workbench
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    McpWorkbench__BindToLoopbackOnly=false \
    McpWorkbench__RegistryPath=/app/data/servers.json
VOLUME ["/app/data"]
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["/app/mcp-workbench"]
