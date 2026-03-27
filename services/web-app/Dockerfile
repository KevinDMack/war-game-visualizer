# war-game-visualizer – web-app
# C# / ASP.NET Core 8 application serving the CesiumJS globe UI.
# Build context: repository root (e.g. docker build -f services/web-app/Dockerfile .)

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/web-app/WebApp.csproj", "services/web-app/"]
RUN dotnet restore "services/web-app/WebApp.csproj"

COPY ["services/web-app/", "services/web-app/"]
RUN dotnet publish "services/web-app/WebApp.csproj" -c Release -o /app/publish --no-restore

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Metadata
LABEL org.opencontainers.image.source="https://github.com/KevinDMack/war-game-visualizer" \
      org.opencontainers.image.description="War Game Visualizer web application"

# Install system dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN groupadd --system appgroup && \
    useradd --system --gid appgroup --no-create-home appuser

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000 \
    DAPR_HTTP_PORT=3500 \
    DAPR_GRPC_PORT=50001 \
    CESIUM_ION_TOKEN="" \
    SCENARIO_SERVICE_APP_ID="scenario-service"

EXPOSE 5000

USER appuser

ENTRYPOINT ["dotnet", "WebApp.dll"]
