# war-game-visualizer – web-app
# Python / Flask application serving the CesiumJS globe UI.
# This image does NOT extend the .NET base image because it is a Python service.

FROM python:3.12-slim AS runtime

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

# Install Python dependencies first (better Docker layer caching)
COPY web-app/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Copy application source
COPY web-app/ ./

# Dapr sidecar ports
ENV DAPR_HTTP_PORT=3500 \
    DAPR_GRPC_PORT=50001 \
    FLASK_APP=app.py \
    FLASK_DEBUG=false

EXPOSE 5000

USER appuser

# Use gunicorn for production; fall back to flask for development
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "2", "--timeout", "60", "app:app"]
