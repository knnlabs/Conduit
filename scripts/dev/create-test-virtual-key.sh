#!/bin/bash
# Script to create a test virtual key

# Get master key
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MASTER_KEY=$("$SCRIPT_DIR/get-master-key.sh")
if [ $? -ne 0 ]; then
    echo "Error: Failed to get master key" >&2
    exit 1
fi

# Create virtual key
KEY_NAME="${1:-SignalR Test Key}"
DESCRIPTION="${2:-Test key for SignalR connections}"

echo "Creating virtual key: $KEY_NAME" >&2

RESPONSE=$(curl -s -X POST http://localhost:5002/api/virtualkeys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d "{
    \"keyName\": \"$KEY_NAME\",
    \"description\": \"$DESCRIPTION\",
    \"isEnabled\": true
  }")

# Check if request was successful
if [ $? -ne 0 ] || [ -z "$RESPONSE" ]; then
    echo "Error: Failed to create virtual key" >&2
    exit 1
fi

# Check for error in response
if echo "$RESPONSE" | grep -q '"error"'; then
    echo "Error from API: $RESPONSE" >&2
    exit 1
fi

# Extract the key
KEY=$(echo "$RESPONSE" | jq -r '.virtualKey // empty' 2>/dev/null)

if [ -z "$KEY" ]; then
    echo "Error: Failed to extract key from response" >&2
    echo "Response: $RESPONSE" >&2
    exit 1
fi

echo "Created virtual key successfully!" >&2
echo "Key Name: $KEY_NAME" >&2
echo "Key Value: $KEY" >&2
echo ""
echo "$KEY"