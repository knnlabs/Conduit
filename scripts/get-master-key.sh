#!/bin/bash
# Script to extract the master key from docker-compose.yml

# Get the master key from docker-compose.yml
MASTER_KEY=$(grep -A20 "admin:" docker-compose.yml | grep "CONDUIT_MASTER_KEY:" | head -1 | awk '{print $2}')

if [ -z "$MASTER_KEY" ]; then
    echo "Error: Could not find CONDUIT_MASTER_KEY in docker-compose.yml" >&2
    exit 1
fi

echo "$MASTER_KEY"