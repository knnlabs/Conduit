#!/bin/bash

# Test script to verify GlobalSettings endpoints are working correctly

echo "Testing GlobalSettings endpoints..."

# Get the master key
MASTER_KEY=$(grep -E '^CONDUITLLM__MASTERKEY=' .env 2>/dev/null | cut -d'=' -f2- | tr -d '"' | head -1)
if [ -z "$MASTER_KEY" ]; then
    echo "❌ Master key not found in .env file"
    exit 1
fi

# Test the corrected endpoint
echo ""
echo "Testing GET /api/GlobalSettings/by-key/MASTER_KEY endpoint..."
curl -s -X GET "http://localhost:5002/api/GlobalSettings/by-key/MASTER_KEY" \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Accept: application/json" | jq '.' || echo "❌ Failed to get setting by key"

echo ""
echo "Testing GET /api/GlobalSettings endpoint..."
curl -s -X GET "http://localhost:5002/api/GlobalSettings" \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Accept: application/json" | jq '.[0:3]' || echo "❌ Failed to get all settings"

echo ""
echo "✅ GlobalSettings endpoint test complete"