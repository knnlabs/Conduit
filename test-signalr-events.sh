#!/bin/bash

echo "Starting SignalR event test..."
echo "1. Starting Node.js client to listen for events..."

# Start the Node.js client in background
node test-signalr-node.js &
CLIENT_PID=$!

# Wait for client to connect
sleep 3

echo -e "\n2. Creating a model mapping to trigger NavigationStateUpdated event..."

# Create a model mapping (this should trigger a NavigationStateUpdated event)
curl -s -X POST http://localhost:5002/api/model-mappings \
  -H "X-API-Key: alpha" \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Test GPT-4",
    "modelIdentifier": "gpt-4-test",
    "providerName": "OpenAI",
    "capabilities": ["chat"],
    "contextWindow": 8192,
    "isEnabled": true
  }' > /dev/null

echo "✓ Created model mapping"

# Wait a bit for event propagation
sleep 2

echo -e "\n3. Updating a virtual key to trigger spend update..."

# Get a virtual key to update
KEY_ID=$(curl -s http://localhost:5002/api/virtualkeys -H "X-API-Key: alpha" | jq -r '.[0].id')

# Update the virtual key
curl -s -X PUT "http://localhost:5002/api/virtualkeys/${KEY_ID}" \
  -H "X-API-Key: alpha" \
  -H "Content-Type: application/json" \
  -d '{
    "maxBudget": 500
  }' > /dev/null

echo "✓ Updated virtual key budget"

# Wait for client to receive events
sleep 3

# Cleanup - delete the test model mapping
MODEL_ID=$(curl -s http://localhost:5002/api/model-mappings -H "X-API-Key: alpha" | jq -r '.[] | select(.modelIdentifier == "gpt-4-test") | .id')
if [ ! -z "$MODEL_ID" ] && [ "$MODEL_ID" != "null" ]; then
    curl -s -X DELETE "http://localhost:5002/api/model-mappings/${MODEL_ID}" -H "X-API-Key: alpha" > /dev/null
    echo "✓ Cleaned up test model mapping"
fi

echo -e "\nWaiting for client to finish..."
wait $CLIENT_PID

echo "Test complete!"