#!/bin/bash
# Script to test WebUI SignalR connection status

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Testing WebUI SignalR Connection Status"
echo "======================================="

# Login to WebUI
echo "1. Getting login page for CSRF token..."
LOGIN_PAGE=$(curl -s http://localhost:5001/login -c /tmp/webui-test-cookies.txt)
CSRF_TOKEN=$(echo "$LOGIN_PAGE" | grep -oE 'name="__RequestVerificationToken" value="[^"]+' | cut -d'"' -f4)

if [ -z "$CSRF_TOKEN" ]; then
    echo "   ❌ Failed to get CSRF token"
    exit 1
fi

echo "   Got CSRF token"
echo "2. Logging in to WebUI..."
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:5001/account/login \
  -d "masterKey=conduit123&__RequestVerificationToken=$CSRF_TOKEN&returnUrl=" \
  -b /tmp/webui-test-cookies.txt \
  -c /tmp/webui-test-cookies.txt \
  -L \
  -w "\nHTTP_CODE:%{http_code}")

HTTP_CODE=$(echo "$LOGIN_RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
echo "   Login HTTP code: $HTTP_CODE"

if [ "$HTTP_CODE" != "200" ] && [ "$HTTP_CODE" != "302" ]; then
    echo "   ❌ Login failed"
    exit 1
fi

# Get the dashboard page
echo -e "\n3. Accessing dashboard..."
DASHBOARD=$(curl -s http://localhost:5001/ \
  -b /tmp/webui-test-cookies.txt \
  -L)

# Check for conduitConfig
echo -e "\n4. Checking conduitConfig..."
CONFIG=$(echo "$DASHBOARD" | grep -oE "window\.conduitConfig = \{[^}]+\}" | head -1)
if [ ! -z "$CONFIG" ]; then
    echo "   ✅ Found: $CONFIG"
else
    echo "   ❌ conduitConfig not found in rendered page"
fi

# Check for connection status elements
echo -e "\n5. Checking for connection status indicators..."
CONNECTION_STATUS=$(echo "$DASHBOARD" | grep -oE "(Connected|Disconnected|Not connected|connection-status)" | sort | uniq -c)
if [ ! -z "$CONNECTION_STATUS" ]; then
    echo "$CONNECTION_STATUS" | sed 's/^/   /'
else
    echo "   ❌ No connection status indicators found"
fi

# Check SignalR scripts
echo -e "\n6. Checking SignalR script tags..."
SIGNALR_SCRIPTS=$(echo "$DASHBOARD" | grep -E "signalr|SignalR" | grep "<script" | wc -l)
echo "   Found $SIGNALR_SCRIPTS SignalR-related script tags"

# Check JavaScript files loaded
echo -e "\n7. JavaScript files loaded:"
JS_FILES=$(echo "$DASHBOARD" | grep -E "<script.*src=" | grep -oE 'src="[^"]+' | cut -d'"' -f2 | grep -E "signalr|conduit")
if [ ! -z "$JS_FILES" ]; then
    echo "$JS_FILES" | sed 's/^/   /'
else
    echo "   No relevant JavaScript files found"
fi

# Check WebUI container logs for recent errors
echo -e "\n8. Recent WebUI errors (last 10 lines):"
docker logs conduit-webui-1 2>&1 | grep -E "(Error|Exception|fail|SignalR)" | tail -10 | sed 's/^/   /'

# Test negotiate endpoint with WebUI key
echo -e "\n9. Testing SignalR negotiate endpoint..."
WEBUI_KEY=$("$SCRIPT_DIR/get-webui-virtual-key.sh" 2>/dev/null)
if [ ! -z "$WEBUI_KEY" ]; then
    NEGOTIATE_RESPONSE=$(curl -s -X POST "http://localhost:5000/hubs/notifications/negotiate?negotiateVersion=1&access_token=$WEBUI_KEY")
    if echo "$NEGOTIATE_RESPONSE" | grep -q "connectionId"; then
        echo "   ✅ Negotiate endpoint working!"
        echo "   Response: $(echo $NEGOTIATE_RESPONSE | jq -c . 2>/dev/null || echo $NEGOTIATE_RESPONSE)"
    else
        echo "   ❌ Negotiate failed: $NEGOTIATE_RESPONSE"
    fi
else
    echo "   ⚠️  Could not get WebUI virtual key"
fi

# Clean up
rm -f /tmp/webui-test-cookies.txt

echo -e "\n============================================"
echo "Test complete. Check the output above."