#!/bin/bash
# Script to create the WebUI virtual key

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Wait for services
"$SCRIPT_DIR/wait-for-services.sh" || exit 1

# Get master key
MASTER_KEY=$("$SCRIPT_DIR/get-master-key.sh")
if [ $? -ne 0 ]; then
    echo "Error: Failed to get master key" >&2
    exit 1
fi

# Check if WebUI Internal Key exists
echo "Checking for existing WebUI Internal Key..." >&2
RESPONSE=$(curl -s -X GET http://localhost:5002/api/virtualkeys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json")

# Look for WebUI Internal Key
KEY_ID=$(echo "$RESPONSE" | jq -r '.[] | select(.keyName == "WebUI Internal Key") | .id // empty' 2>/dev/null)

if [ ! -z "$KEY_ID" ]; then
    echo "WebUI Internal Key already exists (ID: $KEY_ID)" >&2
    echo "Note: The actual key value can only be retrieved when creating the key." >&2
    echo "If you need the key value, please delete and recreate it." >&2
    exit 0
fi

# Create WebUI Internal Key
echo "Creating WebUI Internal Key..." >&2
CREATE_RESPONSE=$(curl -s -X POST http://localhost:5002/api/virtualkeys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "keyName": "WebUI Internal Key",
    "description": "Internal key for WebUI SignalR connections",
    "isEnabled": true,
    "metadata": "{\"purpose\": \"Internal WebUI authentication\", \"createdBy\": \"create-webui-key-script\"}"
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

echo "✅ WebUI Internal Key created successfully!" >&2
echo "" >&2
echo "IMPORTANT: Save this key - it cannot be retrieved again!" >&2
echo "==========================================" >&2
echo "$VIRTUAL_KEY" >&2
echo "==========================================" >&2
echo "" >&2
echo "To configure the WebUI, set this environment variable:" >&2
echo "export CONDUIT_WEBUI_VIRTUAL_KEY=\"$VIRTUAL_KEY\"" >&2

# Save to a temporary file for testing
echo "$VIRTUAL_KEY" > /tmp/webui-virtual-key.txt
echo "" >&2
echo "Key saved to: /tmp/webui-virtual-key.txt" >&2