import { ConduitAdminClient } from '../src';

async function connectionManagementExample() {
  // Initialize the admin client
  const client = new ConduitAdminClient({
    masterKey: process.env.CONDUIT_MASTER_KEY || 'your-master-key',
    adminApiUrl: process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5002',
  });

  try {
    // Example 1: Manually connect SignalR hubs
    console.log('Connecting to SignalR hubs...');
    await client.connection.connect();
    console.log('Connected successfully!');

    // Example 2: Check connection status
    const status = client.connection.getStatus();
    console.log('Connection status:', status);

    // Example 3: Check if any hub is connected
    if (client.connection.isConnected()) {
      console.log('At least one hub is connected');
    }

    // Example 4: Check if all hubs are connected
    if (client.connection.isFullyConnected()) {
      console.log('All hubs are connected');
    }

    // Example 5: Get detailed connection status
    const detailedStatus = client.connection.getDetailedStatus();
    console.log('\nDetailed connection status:');
    detailedStatus.forEach(hub => {
      console.log(`- ${hub.hub}: ${hub.stateDescription}`);
    });

    // Example 6: Connect specific hub
    console.log('\nConnecting navigation hub...');
    await client.connection.connectHub('navigation');
    console.log('Navigation hub connected');

    // Example 7: Check specific hub status
    const navStatus = client.connection.getHubStatus('navigation');
    console.log(`Navigation hub state: ${navStatus}`);

    // Example 8: Disconnect specific hub
    console.log('\nDisconnecting navigation hub...');
    await client.connection.disconnectHub('navigation');
    console.log('Navigation hub disconnected');

    // Example 9: Wait for full connection
    console.log('\nWaiting for all connections (max 15 seconds)...');
    const fullyConnected = await client.connection.waitForConnection(15000);
    if (fullyConnected) {
      console.log('All connections established');
    } else {
      console.log('Connection timeout - some hubs may not be connected');
    }

    // Example 10: Reconnect all hubs
    console.log('\nReconnecting all hubs...');
    await client.connection.reconnect();
    console.log('All hubs reconnected');

    // Example 11: Update SignalR configuration
    console.log('\nUpdating SignalR configuration...');
    await client.connection.updateConfiguration({
      reconnectDelay: [0, 2000, 5000, 10000, 30000], // Custom delays
      connectionTimeout: 25000, // 25 second timeout
    });
    console.log('Configuration updated');

    // Example 12: Disconnect all hubs
    console.log('\nDisconnecting all hubs...');
    await client.connection.disconnect();
    console.log('All hubs disconnected');

  } catch (error) {
    console.error('Connection management error:', error);
  }
}

// Usage with auto-connect disabled
async function manualConnectionExample() {
  // Initialize client with auto-connect disabled
  const client = new ConduitAdminClient({
    masterKey: process.env.CONDUIT_MASTER_KEY || 'your-master-key',
    adminApiUrl: process.env.CONDUIT_ADMIN_API_URL || 'http://localhost:5002',
    options: {
      signalR: {
        enabled: true,
        autoConnect: false, // Disable auto-connect
      }
    }
  });

  try {
    // Check initial status (should be disconnected)
    console.log('Initial connection status:', client.connection.getStatus());
    
    // Connect only the notification hub
    console.log('\nConnecting notification hub only...');
    await client.connection.connectHub('notifications');
    
    // Subscribe to admin notifications
    await client.realtimeNotifications.subscribe({
      onVirtualKeyUpdated: (data) => {
        console.log('Virtual key updated:', data.keyId);
      },
      onProviderHealthChanged: (data) => {
        console.log('Provider health changed:', data.providerName, '->', data.status);
      }
    });
    
    // Later, connect the navigation hub if needed
    console.log('\nConnecting navigation hub...');
    await client.connection.connectHub('navigation');
    
    // ... use the client ...
    
    // Disconnect when done
    await client.connection.disconnect();
    
  } catch (error) {
    console.error('Error:', error);
  }
}

// Run the examples
if (require.main === module) {
  console.log('=== Admin Connection Management Example ===\n');
  connectionManagementExample()
    .then(() => {
      console.log('\n=== Manual Connection Example ===\n');
      return manualConnectionExample();
    })
    .catch(console.error);
}