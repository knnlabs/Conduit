const signalR = require("@microsoft/signalr");

const apiKey = "condt_LC10b6Sij298uRYWAbtf650cn7yoI5ean9TUHUDHIJ4=";
const hubUrl = "http://localhost:5000/hubs/notifications";

console.log(`Connecting to: ${hubUrl}`);
console.log(`Using API Key: ${apiKey.substring(0, 20)}...`);

const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
        accessTokenFactory: () => apiKey
    })
    .configureLogging(signalR.LogLevel.Information)
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

connection.on("NavigationStateChanged", (data) => {
    console.log("🧭 Navigation State Changed:", data);
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
        
        // Subscribe to navigation state updates
        console.log("\n📡 Subscribing to navigation state updates...");
        try {
            await connection.invoke("SubscribeToNavigationUpdates");
            console.log("✅ Subscribed to navigation updates");
        } catch (err) {
            console.error("❌ Failed to subscribe:", err);
        }
        
        // Keep connection alive for 10 seconds
        console.log("\n⏳ Listening for events for 10 seconds...");
        setTimeout(async () => {
            console.log("\n🔌 Closing connection...");
            await connection.stop();
            process.exit(0);
        }, 10000);
        
    } catch (err) {
        console.error("❌ Connection failed:", err);
        process.exit(1);
    }
}

start();