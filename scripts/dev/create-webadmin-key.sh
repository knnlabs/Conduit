#!/bin/bash
# Script to create the WebAdmin virtual key

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Wait for services
"$SCRIPT_DIR/../setup/wait-for-services.sh" || exit 1

# Get master key from environment
if [ -z "$CONDUIT_MASTER_KEY" ]; then
    echo "Error: CONDUIT_MASTER_KEY environment variable is not set" >&2
    echo "Please set CONDUIT_MASTER_KEY in your .env file or environment" >&2
    exit 1
fi
MASTER_KEY="$CONDUIT_MASTER_KEY"
if [ $? -ne 0 ]; then
    echo "Error: Failed to get master key" >&2
    exit 1
fi

# Check if WebAdmin Internal Key exists
echo "Checking for existing WebAdmin Internal Key..." >&2
RESPONSE=$(curl -s -X GET http://localhost:5002/api/virtualkeys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json")

# Look for WebAdmin Internal Key
KEY_ID=$(echo "$RESPONSE" | jq -r '.[] | select(.keyName == "WebAdmin Internal Key") | .id // empty' 2>/dev/null)

if [ ! -z "$KEY_ID" ]; then
    echo "WebAdmin Internal Key already exists (ID: $KEY_ID)" >&2
    echo "Note: The actual key value can only be retrieved when creating the key." >&2
    echo "If you need the key value, please delete and recreate it." >&2
    exit 0
fi

# Create WebAdmin Internal Key
echo "Creating WebAdmin Internal Key..." >&2
CREATE_RESPONSE=$(curl -s -X POST http://localhost:5002/api/virtualkeys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "keyName": "WebAdmin Internal Key",
    "description": "Internal key for WebAdmin SignalR connections",
    "isEnabled": true,
    "metadata": "{\"purpose\": \"Internal WebAdmin authentication\", \"createdBy\": \"create-webadmin-key-script\"}"
  }')

# Check for error
if echo "$CREATE_RESPONSE" | grep -q '"error"'; then
    echo "Error creating key: $CREATE_RESPONSE" >&2
    exit 1
fi

# Extract the key
VIRTUAL_KEY=$(echo "$CREATE_RESPONSE" | jq -r '.virtualKey // empty' 2>/dev/null)

if [ -z "$VIRTUAL_KEY" ]; then
    echo "Error: Failed to extract key from response" >&2
    echo "Response: $CREATE_RESPONSE" >&2
    exit 1
fi

echo "✅ WebAdmin Internal Key created successfully!" >&2
echo "" >&2
echo "IMPORTANT: Save this key - it cannot be retrieved again!" >&2
echo "==========================================" >&2
echo "$VIRTUAL_KEY" >&2
echo "==========================================" >&2
echo "" >&2
echo "To configure the WebAdmin, set this environment variable:" >&2
echo "export CONDUIT_WEBADMIN_VIRTUAL_KEY=\"$VIRTUAL_KEY\"" >&2

# Save to a temporary file for testing
echo "$VIRTUAL_KEY" > /tmp/webadmin-virtual-key.txt
echo "" >&2
echo "Key saved to: /tmp/webadmin-virtual-key.txt" >&2