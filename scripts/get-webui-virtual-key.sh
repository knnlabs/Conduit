#!/bin/bash
# Script to get the WebUI Internal Virtual Key

# First, ensure services are running
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$SCRIPT_DIR/wait-for-services.sh" || exit 1

# Get master key
MASTER_KEY=$("$SCRIPT_DIR/get-master-key.sh")
if [ $? -ne 0 ]; then
    echo "Error: Failed to get master key" >&2
    exit 1
fi

echo "Using master key: $MASTER_KEY" >&2

# Get all virtual keys from Admin API
RESPONSE=$(curl -s -X GET http://localhost:5002/api/virtualkeys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json")

# Check if request was successful
if [ $? -ne 0 ] || [ -z "$RESPONSE" ]; then
    echo "Error: Failed to get virtual keys from Admin API" >&2
    exit 1
fi

# Check for error in response
if echo "$RESPONSE" | grep -q '"error"'; then
    echo "Error from API: $RESPONSE" >&2
    exit 1
fi

# Look for WebUI Internal Key
WEBUI_KEY=$(echo "$RESPONSE" | jq -r '.[] | select(.keyName == "WebUI Internal Key") | .key // empty' 2>/dev/null)

if [ -z "$WEBUI_KEY" ]; then
    echo "Error: WebUI Internal Key not found. Available keys:" >&2
    echo "$RESPONSE" | jq -r '.[].keyName' 2>/dev/null >&2
    exit 1
fi

echo "$WEBUI_KEY"