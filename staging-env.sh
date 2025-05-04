#!/bin/bash

# Configuration file for Conduit staging environment with repository pattern enabled
# Source this file to set up the environment variables for testing

# Basic Conduit configuration
export ASPNETCORE_ENVIRONMENT=Staging
export CONDUIT_USE_REPOSITORY_PATTERN=true
export CONDUIT_DATABASE_ENSURE_CREATED=true

# Database configuration (SQLite by default for easy testing)
export CONDUIT_DATABASE_PROVIDER=sqlite
export CONDUIT_DATABASE_CONNECTION_STRING="Data Source=staging.db"

# Optional: PostgreSQL configuration
# Uncomment these lines to use PostgreSQL instead of SQLite
# export CONDUIT_DATABASE_PROVIDER=postgres
# export CONDUIT_DATABASE_CONNECTION_STRING="Host=localhost;Database=conduit_staging;Username=postgres;Password=postgres"

# Generate a random master key for testing
export CONDUIT_MASTER_KEY=$(openssl rand -hex 16)
echo "Generated master key: $CONDUIT_MASTER_KEY"

# Set up logging
export CONDUIT_LOG_LEVEL=Debug

# Optional: Cache configuration
export CONDUIT_CACHE_ENABLED=true
export CONDUIT_CACHE_DURATION_MINUTES=10

# Router configuration
export CONDUIT_ROUTER_ENABLED=true
export CONDUIT_ROUTER_STRATEGY=RoundRobin

echo "Staging environment configured with repository pattern enabled"
echo "Run the application with: dotnet run --project ConduitLLM.WebUI"