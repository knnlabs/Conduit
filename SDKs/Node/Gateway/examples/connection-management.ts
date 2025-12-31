import { ConduitCoreClient } from '../src';

async function connectionManagementExample() {
  // Initialize the client
  const client = ConduitCoreClient.fromApiKey(
    process.env.CONDUIT_API_KEY || 'your-api-key',
    process.env.CONDUIT_API_URL || 'http://localhost:5000'
  );

  try {
    // Example 1: Manually connect SignalR hubs
    console.log('Connecting to SignalR hubs...');
    await client.connection.connect();
    console.log('Connected successfully!');

    // Example 2: Check connection status
    const status = client.connection.getStatus();
    console.log('Connection status:', status);

    // Example 3: Check if connected
    if (client.connection.isConnected()) {
      console.log('All hubs are connected');
    }

    // Example 4: Get detailed connection status
    const detailedStatus = client.connection.getDetailedStatus();
    console.log('\nDetailed connection status:');
    detailedStatus.forEach(hub => {
      console.log(`- ${hub.hub}: ${hub.stateDescription}`);
    });

    // Example 5: Wait for connection with timeout
    console.log('\nWaiting for connection (max 10 seconds)...');
    const connected = await client.connection.waitForConnection(10000);
    if (connected) {
      console.log('Connection established within timeout');
    } else {
      console.log('Connection timeout reached');
    }

    // Example 6: Reconnect hubs
    console.log('\nReconnecting...');
    await client.connection.reconnect();
    console.log('Reconnected successfully');

    // Example 7: Update SignalR configuration
    console.log('\nUpdating SignalR configuration...');
    await client.connection.updateConfiguration({
      reconnectDelay: [0, 1000, 5000, 10000], // Custom reconnect delays
      connectionTimeout: 20000, // 20 second timeout
    });
    console.log('Configuration updated');

    // Example 8: Disconnect when done
    console.log('\nDisconnecting...');
    await client.connection.disconnect();
    console.log('Disconnected successfully');

  } catch (error) {
    console.error('Connection management error:', error);
  }
}

// Usage with auto-connect disabled
async function manualConnectionExample() {
  // Initialize client with auto-connect disabled
  const client = new ConduitCoreClient({
    apiKey: process.env.CONDUIT_API_KEY || 'your-api-key',
    baseURL: process.env.CONDUIT_API_URL || 'http://localhost:5000',
    signalR: {
      enabled: true,
      autoConnect: false, // Disable auto-connect
    }
  });

  try {
    // Manually connect when ready
    console.log('Manually connecting to SignalR...');
    await client.connection.connect();
    
    // Use the client for real-time operations
    // ... your code here ...
    
    // Disconnect when done
    await client.connection.disconnect();
    
  } catch (error) {
    console.error('Error:', error);
  }
}

// Run the examples
if (require.main === module) {
  console.log('=== Connection Management Example ===\n');
  connectionManagementExample()
    .then(() => {
      console.log('\n=== Manual Connection Example ===\n');
      return manualConnectionExample();
    })
    .catch(console.error);
}