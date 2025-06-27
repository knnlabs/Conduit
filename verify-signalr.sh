#!/bin/bash

# Create a new test key
echo "Creating fresh test key..."
CREATE_RESPONSE=$(curl -s -X POST http://localhost:5002/api/virtualkeys \
  -H "X-API-Key: alpha" \
  -H "Content-Type: application/json" \
  -d '{"keyName": "SignalR Verification Test", "isEnabled": true}')

KEY=$(echo "$CREATE_RESPONSE" | jq -r '.virtualKey')
KEY_ID=$(echo "$CREATE_RESPONSE" | jq -r '.keyInfo.id')

if [ -z "$KEY" ] || [ "$KEY" = "null" ]; then
    echo "❌ Failed to create test key"
    exit 1
fi

echo "✓ Created test key: ${KEY:0:20}..."

# URL encode the key
URL_ENCODED_KEY=$(printf '%s' "$KEY" | jq -sRr @uri)

# Test negotiate endpoint
echo -e "\nTesting negotiate endpoint..."
NEGOTIATE_RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X POST \
  "http://localhost:5000/hubs/notifications/negotiate?negotiateVersion=1&access_token=${URL_ENCODED_KEY}" \
  -H "Content-Length: 0")

HTTP_STATUS=$(echo "$NEGOTIATE_RESPONSE" | grep "HTTP_STATUS:" | cut -d: -f2)
BODY=$(echo "$NEGOTIATE_RESPONSE" | sed '/HTTP_STATUS:/d')

if [ "$HTTP_STATUS" = "200" ]; then
    echo "✓ Negotiate successful (HTTP 200)"
    CONNECTION_ID=$(echo "$BODY" | jq -r '.connectionId')
    echo "  Connection ID: $CONNECTION_ID"
else
    echo "❌ Negotiate failed (HTTP $HTTP_STATUS)"
    echo "  Response: $BODY"
fi

# Test WebSocket connection
echo -e "\nTesting WebSocket connection..."
if command -v wscat &> /dev/null; then
    # Create a test script that connects and waits for messages
    cat > /tmp/ws-test.sh << EOF
#!/bin/bash
echo "Connecting to WebSocket..."
(
    # Send handshake
    echo '{"protocol":"json","version":1}'
    # Wait for handshake response
    sleep 2
    # Send ping
    echo '{"type":6}'
    # Wait a bit more
    sleep 3
) | timeout 10 wscat -c "ws://localhost:5000/hubs/notifications?access_token=${URL_ENCODED_KEY}" 2>&1 | while IFS= read -r line; do
    echo "WS: \$line"
    if echo "\$line" | grep -q "type.*1"; then
        echo "✓ Handshake successful!"
    fi
    if echo "\$line" | grep -q "error"; then
        echo "❌ WebSocket error detected"
    fi
done
EOF
    
    chmod +x /tmp/ws-test.sh
    /tmp/ws-test.sh
    rm /tmp/ws-test.sh
else
    echo "⚠️  wscat not installed, skipping WebSocket test"
fi

# Clean up - delete the test key
echo -e "\nCleaning up..."
DELETE_RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X DELETE \
  "http://localhost:5002/api/virtualkeys/${KEY_ID}" \
  -H "X-API-Key: alpha")

DELETE_STATUS=$(echo "$DELETE_RESPONSE" | grep "HTTP_STATUS:" | cut -d: -f2)
if [ "$DELETE_STATUS" = "204" ]; then
    echo "✓ Test key deleted"
else
    echo "⚠️  Failed to delete test key (HTTP $DELETE_STATUS)"
fi

echo -e "\n============================================"
echo "VERIFICATION COMPLETE"
echo "============================================"