#!/bin/bash

# Start database and API containers in detached mode
echo "Starting Postgres and API containers..."
docker-compose up -d postgres api

# Wait for services to be ready
echo "Waiting for services to initialize..."
sleep 10

# Build the WebUI project first to catch any compilation errors
echo "Building WebUI project..."
dotnet build ConduitLLM.WebUI

# Check if build succeeded
if [ $? -ne 0 ]; then
    echo "Build failed. Exiting."
    exit 1
fi

# Run WebUI locally with necessary environment variables
echo "Starting WebUI locally..."
export DATABASE_URL="postgresql://conduit:conduitpass@localhost:5432/conduitdb"
export CONDUIT_MASTER_KEY="alpha"
export CONDUIT_INSECURE="true"
export CONDUIT_API_BASE_URL="http://localhost:5000"
export CONDUIT_DATABASE_ENSURE_CREATED="true"  # This forces schema creation without migrations
export ASPNETCORE_URLS="http://localhost:5002"
export ASPNETCORE_ENVIRONMENT="Development"  # Run in development mode

dotnet run --project ConduitLLM.WebUI