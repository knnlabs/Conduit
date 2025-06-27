#!/bin/bash

echo "Verifying WebUI SignalR connection..."

# Login to WebUI
echo "1. Logging in to WebUI..."
RESPONSE=$(curl -s -X POST http://localhost:5001/login \
  -d "AuthKey=conduit123" \
  -c /tmp/webui-cookies.txt \
  -L \
  -w "\nHTTP_CODE:%{http_code}")

HTTP_CODE=$(echo "$RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
echo "   Login HTTP code: $HTTP_CODE"

# Get the dashboard page
echo -e "\n2. Accessing dashboard..."
DASHBOARD=$(curl -s http://localhost:5001/ \
  -b /tmp/webui-cookies.txt \
  -L)

# Check for conduitConfig
echo -e "\n3. Checking conduitConfig..."
CONFIG=$(echo "$DASHBOARD" | grep -oE "window\.conduitConfig = \{[^}]+\}" | head -1)
if [ ! -z "$CONFIG" ]; then
    echo "   Found: $CONFIG"
else
    echo "   ❌ conduitConfig not found in rendered page"
fi

# Check for connection status elements
echo -e "\n4. Checking for connection status indicators..."
CONNECTION_STATUS=$(echo "$DASHBOARD" | grep -oE "(Connected|Disconnected|Not connected|connection-status)" | sort | uniq -c)
if [ ! -z "$CONNECTION_STATUS" ]; then
    echo "$CONNECTION_STATUS"
else
    echo "   ❌ No connection status indicators found"
fi

# Check SignalR scripts
echo -e "\n5. Checking SignalR script tags..."
SIGNALR_SCRIPTS=$(echo "$DASHBOARD" | grep -E "signalr|SignalR" | grep "<script" | wc -l)
echo "   Found $SIGNALR_SCRIPTS SignalR-related script tags"

# Save the dashboard for inspection
echo "$DASHBOARD" > /tmp/webui-dashboard-full.html
echo -e "\nFull dashboard saved to /tmp/webui-dashboard-full.html"

# Check WebUI container logs for errors
echo -e "\n6. Recent WebUI errors:"
docker logs conduit-webui-1 2>&1 | grep -E "(Error|Exception|fail)" | tail -5

echo -e "\n============================================"
echo "Diagnosis complete. Check the output above."