#!/bin/bash
# Script to get the WebAdmin Internal Virtual Key

# First, ensure services are running
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
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

echo "Using master key: $MASTER_KEY" >&2

# Try to get the WebAdmin virtual key from GlobalSettings
RESPONSE=$(curl -s -X GET "http://localhost:5002/api/GlobalSettings/by-key/WebAdmin_VirtualKey" \
  -H "X-API-Key: $MASTER_KEY" \
  -H "Content-Type: application/json")

# Check if request was successful and key exists
if [ $? -eq 0 ] && [ ! -z "$RESPONSE" ] && ! echo "$RESPONSE" | grep -q '"error"' && ! echo "$RESPONSE" | grep -q 'null'; then
    # Extract the virtual key from GlobalSettings
    WEBADMIN_KEY=$(echo "$RESPONSE" | jq -r '.value // empty' 2>/dev/null)
    
    if [ ! -z "$WEBADMIN_KEY" ]; then
        echo "Found WebAdmin virtual key in GlobalSettings" >&2
        echo "$WEBADMIN_KEY"
        exit 0
    fi
fi

echo "WebAdmin virtual key not found in GlobalSettings. Creating new one..." >&2

# First, ensure we have a default virtual key group
GROUP_RESPONSE=$(curl -s -X GET http://localhost:5002/api/VirtualKeyGroups \
  -H "X-API-Key: $MASTER_KEY" \
  -H "Content-Type: application/json")

GROUP_ID=$(echo "$GROUP_RESPONSE" | jq -r '.[0].id // empty' 2>/dev/null)

if [ -z "$GROUP_ID" ]; then
    echo "Creating default virtual key group..." >&2
    GROUP_RESPONSE=$(curl -s -X POST http://localhost:5002/api/VirtualKeyGroups \
      -H "X-API-Key: $MASTER_KEY" \
      -H "Content-Type: application/json" \
      -d '{
        "name": "Default Group",
        "description": "Default virtual key group"
      }')
    GROUP_ID=$(echo "$GROUP_RESPONSE" | jq -r '.id // empty' 2>/dev/null)
fi

# Create a new virtual key
echo "Creating new 'WebAdmin Internal Key'..." >&2
CREATE_PAYLOAD=$(cat <<EOF
{
    "keyName": "WebAdmin Internal Key",
    "allowedModels": null,
    "maxBudget": null,
    "budgetDuration": null,
    "expiresAt": null,
    "virtualKeyGroupId": $GROUP_ID,
    "metadata": "{\"purpose\": \"Internal WebAdmin authentication\"}",
    "rateLimitRpm": null,
    "rateLimitRpd": null
}
EOF
)

CREATE_RESPONSE=$(curl -s -X POST http://localhost:5002/api/VirtualKeys \
  -H "X-API-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d "$CREATE_PAYLOAD")

# Check for curl error or API error
if [ $? -ne 0 ] || echo "$CREATE_RESPONSE" | grep -q '"error"'; then
    echo "Error: Failed to create new WebAdmin key. API response:" >&2
    echo "$CREATE_RESPONSE" >&2
    exit 1
fi

# Extract the new key
WEBADMIN_KEY=$(echo "$CREATE_RESPONSE" | jq -r '.virtualKey // empty')

if [ -z "$WEBADMIN_KEY" ]; then
    echo "Error: Failed to extract new key from API response." >&2
    echo "$CREATE_RESPONSE" >&2
    exit 1
fi

# Store the key in GlobalSettings for future use
echo "Storing WebAdmin key in GlobalSettings..." >&2
STORE_PAYLOAD=$(cat <<EOF
{
    "key": "WebAdmin_VirtualKey",
    "value": "$WEBADMIN_KEY",
    "description": "Virtual key for WebAdmin Gateway API access"
}
EOF
)

STORE_RESPONSE=$(curl -s -X POST http://localhost:5002/api/GlobalSettings \
  -H "X-API-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d "$STORE_PAYLOAD")

if [ $? -eq 0 ] && ! echo "$STORE_RESPONSE" | grep -q '"error"'; then
    echo "Successfully stored WebAdmin key in GlobalSettings." >&2
else
    echo "Warning: Failed to store key in GlobalSettings, but key was created." >&2
fi

echo "$WEBADMIN_KEY"