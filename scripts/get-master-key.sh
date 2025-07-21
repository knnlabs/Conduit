#!/bin/bash
# Script to extract the backend auth key from docker-compose.yml

# Get the backend auth key from docker-compose.yml
BACKEND_KEY=$(grep -A20 "admin:" docker-compose.yml | grep "CONDUIT_API_TO_API_BACKEND_AUTH_KEY:" | head -1 | awk '{print $2}')

if [ -z "$BACKEND_KEY" ]; then
    echo "Error: Could not find CONDUIT_API_TO_API_BACKEND_AUTH_KEY in docker-compose.yml" >&2
    exit 1
fi

echo "$BACKEND_KEY"