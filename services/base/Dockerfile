# Base image for all .NET C# services in the war-game-visualizer application.
# Services extend this image with their own application layer.

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution / project files and restore dependencies (cached layer)
COPY ["*.sln", "./"]
COPY ["**/*.csproj", "./"]
RUN find . -name "*.csproj" | while read f; do \
      dir=$(dirname "$f"); \
      mkdir -p "$dir"; \
      mv "$f" "$dir/"; \
    done

RUN dotnet restore

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install curl (used by Dapr health checks) and the Dapr sidecar CLI
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Create a non-root user for running services
RUN groupadd --system appgroup && \
    useradd --system --gid appgroup --no-create-home appuser

WORKDIR /app

# Set default environment variables
ENV ASPNETCORE_URLS=http://+:5001 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DAPR_HTTP_PORT=3500 \
    DAPR_GRPC_PORT=50001

# Expose the default ASP.NET Core port and Dapr sidecar ports
EXPOSE 5001
EXPOSE 3500
EXPOSE 50001

USER appuser

# Derived images must override ENTRYPOINT with their own published output
# e.g.: COPY --from=build /app/publish .
#       ENTRYPOINT ["dotnet", "MyService.dll"]
