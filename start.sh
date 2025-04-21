#!/bin/bash

# Check if --no-master-key flag is provided
if [ "$1" == "--no-master-key" ]; then
  echo "Starting without generating a Master Key."
else
  if [ -z "$CONDUIT_MASTER_KEY" ]; then
    # Generate a 32-byte (256-bit) random key and convert to hex
    GENERATED_MASTER_KEY=$(openssl rand -hex 32)
    export CONDUIT_MASTER_KEY=$GENERATED_MASTER_KEY
    echo "--------------------------------------------------"
    echo "Generated Master Key (set as CONDUIT_MASTER_KEY env var):"
    echo "$GENERATED_MASTER_KEY"
    echo "--------------------------------------------------"
    echo "Starting services with new Master Key..."
  else
    echo "Using existing CONDUIT_MASTER_KEY."
    echo "Starting services with provided Master Key..."
  fi
fi

echo "Using launch settings from ./ConduitLLM.WebUI/Properties/launchSettings.json..."
echo "Building..."

# Set port environment variables for the WebUI project
export WebUIHttpPort="${WebUIHttpPort:-5001}"
export WebUIHttpsPort="${WebUIHttpsPort:-5002}"

# Set port environment variables for the Http project
export HttpApiHttpPort="${HttpApiHttpPort:-5000}"
export HttpApiHttpsPort="${HttpApiHttpsPort:-5003}"

# Set development environment
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

echo "Starting ConduitLLM with the following ports:"
echo "WebUI HTTP: ${WebUIHttpPort}"
echo "WebUI HTTPS: ${WebUIHttpsPort}"
echo "HTTP API HTTP: ${HttpApiHttpPort}"
echo "HTTP API HTTPS: ${HttpApiHttpsPort}"
echo "Environment: ${ASPNETCORE_ENVIRONMENT}"

# Run in separate terminals or use & to run in background
# Run the Http project with its own URLs
ASPNETCORE_URLS="http://127.0.0.1:${HttpApiHttpPort};https://127.0.0.1:${HttpApiHttpsPort}" dotnet run --project ./ConduitLLM.Http &

# Run the WebUI project with its own URLs
ASPNETCORE_URLS="http://127.0.0.1:${WebUIHttpPort};https://127.0.0.1:${WebUIHttpsPort}" dotnet run --project ./ConduitLLM.WebUI
