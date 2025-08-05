#!/bin/bash
# Script to fix the WebUI virtual key

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Get master key
MASTER_KEY=$("$SCRIPT_DIR/get-master-key.sh")
if [ $? -ne 0 ]; then
    echo "Error: Failed to get master key" >&2
    exit 1
fi

echo "Creating new WebUI Internal Key..."

# First, ensure we have a default virtual key group
GROUP_RESPONSE=$(curl -s -X GET http://localhost:5002/api/VirtualKeyGroups \
  -H "X-API-Key: $MASTER_KEY" \
  -H "Content-Type: application/json")

GROUP_ID=$(echo "$GROUP_RESPONSE" | jq -r '.[0].id // empty' 2>/dev/null)

if [ -z "$GROUP_ID" ]; then
    echo "Creating default virtual key group..."
    GROUP_RESPONSE=$(curl -s -X POST http://localhost:5002/api/VirtualKeyGroups \
      -H "X-API-Key: $MASTER_KEY" \
      -H "Content-Type: application/json" \
      -d '{
        "name": "Default Group",
        "description": "Default virtual key group"
      }')
    GROUP_ID=$(echo "$GROUP_RESPONSE" | jq -r '.id // empty' 2>/dev/null)
    
    if [ -z "$GROUP_ID" ]; then
        echo "Error: Failed to create virtual key group" >&2
        echo "Response: $GROUP_RESPONSE" >&2
        exit 1
    fi
fi

# Create a new WebUI Internal Key
RESPONSE=$(curl -s -X POST http://localhost:5002/api/VirtualKeys \
  -H "X-API-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d "{
    \"keyName\": \"WebUI Internal Key (Fixed)\",
    \"description\": \"Internal key for WebUI SignalR connections\",
    \"isEnabled\": true,
    \"virtualKeyGroupId\": $GROUP_ID,
    \"metadata\": \"{\\\"purpose\\\": \\\"Internal WebUI authentication\\\", \\\"createdBy\\\": \\\"fix-script\\\"}\"
  }")

# Check for error
if echo "$RESPONSE" | grep -q '"error"'; then
    echo "Error creating key: $RESPONSE" >&2
    exit 1
fi

# Extract the key and ID
VIRTUAL_KEY=$(echo "$RESPONSE" | jq -r '.virtualKey // empty' 2>/dev/null)
KEY_ID=$(echo "$RESPONSE" | jq -r '.keyInfo.id // empty' 2>/dev/null)

if [ -z "$VIRTUAL_KEY" ] || [ -z "$KEY_ID" ]; then
    echo "Error: Failed to extract key from response" >&2
    echo "Response: $RESPONSE" >&2
    exit 1
fi

echo "✅ Created new WebUI key with ID: $KEY_ID"
echo "Key: ${VIRTUAL_KEY:0:20}..."

# Update the database directly
echo ""
echo "Updating GlobalSettings in database..."
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "
UPDATE \"GlobalSettings\" 
SET \"Value\" = '$VIRTUAL_KEY' 
WHERE \"Key\" = 'WebUI_VirtualKey';

UPDATE \"GlobalSettings\" 
SET \"Value\" = '$KEY_ID' 
WHERE \"Key\" = 'WebUI_VirtualKeyId';
" >/dev/null 2>&1

if [ $? -eq 0 ]; then
    echo "✅ Database updated successfully!"
else
    echo "❌ Failed to update database"
    exit 1
fi

# Clear Redis cache to ensure fresh data
echo ""
echo "Clearing Redis cache..."
docker exec conduit-redis-1 redis-cli FLUSHDB >/dev/null 2>&1

echo ""
echo "✅ WebUI virtual key fixed successfully!"
echo ""
echo "Virtual Key: $VIRTUAL_KEY"
echo ""
echo "You may need to restart the WebUI service for changes to take effect:"
echo "docker compose restart webui"