#!/bin/bash
# Script to test full SignalR connection with Node.js client

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Get a virtual key
echo "Getting virtual key..." >&2
VIRTUAL_KEY=$("$SCRIPT_DIR/get-webui-virtual-key.sh" 2>/dev/null)

if [ -z "$VIRTUAL_KEY" ]; then
    echo "WebUI key not found, creating test key..." >&2
    VIRTUAL_KEY=$("$SCRIPT_DIR/create-test-virtual-key.sh" "SignalR Connection Test" 2>/dev/null)
fi

if [ -z "$VIRTUAL_KEY" ]; then
    echo "Error: Could not obtain a virtual key" >&2
    exit 1
fi

echo "Using Virtual Key: ${VIRTUAL_KEY:0:20}..." >&2
echo "" >&2

# Create temporary Node.js test script
TEMP_SCRIPT="/tmp/test-signalr-connection.js"

cat > "$TEMP_SCRIPT" << 'EOF'
const signalR = require("@microsoft/signalr");

const apiKey = process.argv[2];
const hubUrl = process.argv[3] || "http://localhost:5000/hubs/notifications";

if (!apiKey) {
    console.error("Usage: node test-signalr-connection.js <api-key> [hub-url]");
    process.exit(1);
}

console.log(`Connecting to: ${hubUrl}`);
console.log(`Using API Key: ${apiKey.substring(0, 20)}...`);

const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
        accessTokenFactory: () => apiKey
    })
    .configureLogging(signalR.LogLevel.Debug)
    .build();

// Set up event handlers
connection.on("ReceiveSystemNotification", (notification) => {
    console.log("📢 System Notification:", notification);
});

connection.on("ModelMappingChanged", (data) => {
    console.log("🔄 Model Mapping Changed:", data);
});

connection.on("ProviderHealthChanged", (data) => {
    console.log("💊 Provider Health Changed:", data);
});

connection.onreconnecting((error) => {
    console.log("🔄 Reconnecting...", error);
});

connection.onreconnected((connectionId) => {
    console.log("✅ Reconnected with ID:", connectionId);
});

connection.onclose((error) => {
    console.log("❌ Connection closed", error);
});

// Start connection
async function start() {
    try {
        await connection.start();
        console.log("✅ Connected successfully!");
        console.log("Connection ID:", connection.connectionId);
        console.log("Connection State:", connection.state);
        
        // Keep connection alive for 10 seconds
        console.log("\nListening for events for 10 seconds...");
        setTimeout(async () => {
            console.log("\nClosing connection...");
            await connection.stop();
            process.exit(0);
        }, 10000);
        
    } catch (err) {
        console.error("❌ Connection failed:", err);
        process.exit(1);
    }
}

start();
EOF

# Check if @microsoft/signalr is installed
if ! npm list @microsoft/signalr >/dev/null 2>&1; then
    echo "Installing @microsoft/signalr..." >&2
    npm install @microsoft/signalr >/dev/null 2>&1
fi

# Run the test
echo "Testing SignalR connection..." >&2
echo "=============================" >&2
node "$TEMP_SCRIPT" "$VIRTUAL_KEY" "$1"

# Clean up
rm -f "$TEMP_SCRIPT"