# war-game-visualizer – scenario-service
# ASP.NET Core 8 gRPC service handling CRUD operations for wargame scenarios.
# Build context: repository root (e.g. docker build -f services/scenario-service/Dockerfile .)

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy library project first for layer caching
COPY ["libraries/wargame.data/wargame.data.csproj", "libraries/wargame.data/"]
COPY ["services/scenario-service/ScenarioService.csproj", "services/scenario-service/"]
COPY ["protos/", "protos/"]

RUN dotnet restore "services/scenario-service/ScenarioService.csproj"

COPY ["libraries/wargame.data/", "libraries/wargame.data/"]
COPY ["services/scenario-service/", "services/scenario-service/"]

RUN dotnet publish "services/scenario-service/ScenarioService.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- Runtime stage ----------
# Extend the shared base image which already installs curl, ca-certificates,
# creates the non-root appuser, and sets default Dapr environment variables.
ARG BASE_IMAGE=base:latest
FROM ${BASE_IMAGE} AS runtime

# Metadata
LABEL org.opencontainers.image.source="https://github.com/KevinDMack/war-game-visualizer" \
      org.opencontainers.image.description="War Game Visualizer scenario service"

COPY --from=build /app/publish .

ENV SQL_CONNECTION_STRING=""

ENTRYPOINT ["dotnet", "ScenarioService.dll"]
