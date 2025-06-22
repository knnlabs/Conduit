#!/bin/bash

# Test script for RabbitMQ event flow in Conduit

echo "=== RabbitMQ Event Flow Test ==="
echo

# Check if services are running
echo "1. Checking service health..."
echo "   Core API Health:"
curl -s http://localhost:5000/health/ready | jq -r '.checks[] | select(.name == "rabbitmq") | "   - RabbitMQ: " + .status + " - " + .description'
echo "   Admin API Health:"
curl -s http://localhost:5002/health/ready | jq -r '.checks[] | select(.name == "rabbitmq") | "   - RabbitMQ: " + .status + " - " + .description'
echo

# Get master key
MASTER_KEY="alpha"

# Create a virtual key via Admin API
echo "2. Creating a virtual key via Admin API..."
RESPONSE=$(curl -s -X POST http://localhost:5002/api/virtual-keys \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "RabbitMQ Test Key",
    "description": "Testing RabbitMQ event flow",
    "isEnabled": true,
    "monthlyBudget": 100.00,
    "dailyBudget": 10.00,
    "models": ["gpt-4", "gpt-3.5-turbo"]
  }')

KEY_ID=$(echo $RESPONSE | jq -r '.id')
KEY_HASH=$(echo $RESPONSE | jq -r '.keyHash')

echo "   Created Virtual Key ID: $KEY_ID"
echo "   Key Hash: $KEY_HASH"
echo

# Give time for event to propagate
echo "3. Waiting for event propagation..."
sleep 2

# Update the virtual key to trigger an update event
echo "4. Updating virtual key to trigger VirtualKeyUpdated event..."
UPDATE_RESPONSE=$(curl -s -X PUT http://localhost:5002/api/virtual-keys/$KEY_ID \
  -H "X-Master-Key: $MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "RabbitMQ Test Key - Updated",
    "description": "Updated to test event propagation",
    "isEnabled": true,
    "monthlyBudget": 200.00,
    "dailyBudget": 20.00,
    "models": ["gpt-4", "gpt-3.5-turbo", "claude-3-opus"]
  }')

echo "   Updated Virtual Key"
echo

# Check RabbitMQ queues
echo "5. Checking RabbitMQ queues..."
docker compose exec -T rabbitmq rabbitmqctl list_queues name messages consumers | grep -E "(virtual-key|provider|image|spend)" || echo "   No Conduit queues found yet"
echo

# Test the key via Core API
echo "6. Testing virtual key via Core API (to verify cache was invalidated)..."
TEST_RESPONSE=$(curl -s -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer $KEY_HASH" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-3.5-turbo",
    "messages": [{"role": "user", "content": "Say hello"}],
    "max_tokens": 10
  }')

if echo "$TEST_RESPONSE" | jq -e '.choices[0].message.content' > /dev/null 2>&1; then
  echo "   ✓ Virtual key is working correctly"
  echo "   Response: $(echo "$TEST_RESPONSE" | jq -r '.choices[0].message.content')"
else
  echo "   ✗ Error using virtual key:"
  echo "$TEST_RESPONSE" | jq '.'
fi
echo

# Check logs for event processing
echo "7. Checking for event processing in logs..."
echo "   Recent event-related logs:"
docker compose logs --tail=50 api 2>/dev/null | grep -E "(Event bus configured|VirtualKeyUpdated|Cache invalidated)" | tail -5 || echo "   No event logs found"
echo

# Clean up - delete the test key
echo "8. Cleaning up - deleting test virtual key..."
curl -s -X DELETE http://localhost:5002/api/virtual-keys/$KEY_ID \
  -H "X-Master-Key: $MASTER_KEY"
echo "   Deleted Virtual Key ID: $KEY_ID"
echo

echo "=== Test Complete ==="
echo
echo "To monitor RabbitMQ in real-time:"
echo "  - Management UI: http://localhost:15672 (conduit/conduitpass)"
echo "  - Watch queues: docker compose exec rabbitmq watch rabbitmqctl list_queues"
echo "  - View logs: docker compose logs -f rabbitmq"