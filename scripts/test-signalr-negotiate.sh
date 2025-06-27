#!/bin/bash
# Script to test SignalR negotiate endpoint

# Get a virtual key (create one if needed)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Try to get WebUI key first
echo "Attempting to get WebUI Internal Key..." >&2
VIRTUAL_KEY=$("$SCRIPT_DIR/get-webui-virtual-key.sh" 2>/dev/null)

if [ -z "$VIRTUAL_KEY" ]; then
    echo "WebUI key not found, creating test key..." >&2
    VIRTUAL_KEY=$("$SCRIPT_DIR/create-test-virtual-key.sh" "SignalR Negotiate Test" 2>/dev/null)
fi

if [ -z "$VIRTUAL_KEY" ]; then
    echo "Error: Could not obtain a virtual key" >&2
    exit 1
fi

echo "Using Virtual Key: ${VIRTUAL_KEY:0:20}..." >&2
echo "" >&2

# Test negotiate endpoint
echo "Testing /hubs/notifications/negotiate endpoint:" >&2
echo "==========================================" >&2

curl -X POST -v "http://localhost:5000/hubs/notifications/negotiate?negotiateVersion=1&access_token=$VIRTUAL_KEY" 2>&1 | grep -E "(HTTP/|negotiationVersion|connectionId|connectionToken|availableTransports|{|})"|sed 's/^/  /'

echo ""
echo ""
echo "Testing /hubs/video-generation/negotiate endpoint:" >&2
echo "==========================================" >&2

curl -X POST -v "http://localhost:5000/hubs/video-generation/negotiate?negotiateVersion=1&access_token=$VIRTUAL_KEY" 2>&1 | grep -E "(HTTP/|negotiationVersion|connectionId|connectionToken|availableTransports|{|})" | sed 's/^/  /'