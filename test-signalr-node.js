const signalR = require('@microsoft/signalr');

// Get key from command line or use test key
const key = process.argv[2] || 'condt_0pxETIi99Kt87iT96I+ATxOjPiW9J7KyqdG+BOU0XAM=';

console.log('Testing SignalR connection with Node.js client...');
console.log('Key:', key.substring(0, 20) + '...');

const connection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5000/hubs/notifications', {
        accessTokenFactory: () => key,
        withCredentials: true,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Debug)
    .build();

// Set up event handlers
connection.on("ProviderHealthChanged", (data) => {
    console.log('✓ Received ProviderHealthChanged:', data);
});

connection.on("ModelCapabilitiesDiscovered", (data) => {
    console.log('✓ Received ModelCapabilitiesDiscovered:', data);
});

connection.on("NavigationStateUpdated", (data) => {
    console.log('✓ Received NavigationStateUpdated:', data);
});

connection.on("SystemAnnouncement", (data) => {
    console.log('✓ Received SystemAnnouncement:', data);
});

// Handle connection lifecycle
connection.onclose((error) => {
    if (error) {
        console.error('❌ Connection closed with error:', error);
    } else {
        console.log('Connection closed');
    }
});

connection.onreconnecting((error) => {
    console.log('Reconnecting...', error);
});

connection.onreconnected((connectionId) => {
    console.log('✓ Reconnected with ID:', connectionId);
});

// Start connection
console.log('Starting connection...');
connection.start()
    .then(() => {
        console.log('✅ Connected successfully!');
        console.log('Connection ID:', connection.connectionId);
        console.log('Connection State:', connection.state);
        console.log('\nWaiting for messages (10 seconds)...');
        
        // Keep connection open for 10 seconds
        setTimeout(() => {
            console.log('\nClosing connection...');
            connection.stop();
            process.exit(0);
        }, 10000);
    })
    .catch(err => {
        console.error('❌ Connection failed:', err);
        process.exit(1);
    });