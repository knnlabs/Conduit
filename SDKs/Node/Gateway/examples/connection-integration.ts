import { ConduitCoreClient } from '../src';

/**
 * Integration example showing how to use the connection namespace
 * with real-time features like video generation monitoring.
 */
async function connectionIntegrationExample() {
  const client = new ConduitCoreClient({
    apiKey: process.env.CONDUIT_API_KEY || 'your-api-key',
    baseURL: process.env.CONDUIT_API_URL || 'http://localhost:5000',
    signalR: {
      enabled: true,
      autoConnect: false, // We'll manually control connections
    }
  });

  try {
    // Step 1: Check if we need to connect
    console.log('Checking connection status...');
    if (!client.connection.isConnected()) {
      console.log('Not connected. Establishing connections...');
      
      // Wait for connection with a 10 second timeout
      const connected = await client.connection.waitForConnection(10000);
      
      if (!connected) {
        console.error('Failed to establish connections within timeout');
        // Could fall back to polling-based approach here
        return;
      }
    }

    console.log('Connection established!');
    
    // Step 2: Show detailed connection status
    const detailedStatus = client.connection.getDetailedStatus();
    console.log('\nConnection Details:');
    detailedStatus.forEach(hub => {
      console.log(`  ${hub.hub}: ${hub.stateDescription}`);
    });

    // Step 3: Start a video generation task
    console.log('\nStarting video generation...');
    const videoResponse = await client.videos.generateAsync({
      model: 'minimax-video',
      prompt: 'A serene mountain landscape with clouds',
      resolution: '1280x720',
      duration: 3,
    });

    const taskId = videoResponse.task_id;
    console.log(`Task started: ${taskId}`);

    // Step 4: Subscribe to real-time updates
    const videoHub = client.signalr.getVideoGenerationHubClient();
    
    videoHub.onVideoGenerationStarted = (event) => {
      console.log(`\n[Real-time] Generation started for task ${event.taskId}`);
    };

    videoHub.onVideoGenerationProgress = (event) => {
      console.log(`[Real-time] Progress: ${event.progress}% for task ${event.taskId}`);
    };

    videoHub.onVideoGenerationCompleted = (event) => {
      console.log(`[Real-time] Generation completed!`);
      console.log(`  Video URL: ${event.videoUrl}`);
      console.log(`  Duration: ${event.duration}s`);
    };

    // Subscribe to the specific task
    await client.signalr.subscribeToTask(taskId, 'video');

    // Step 5: Also poll for status (as backup)
    console.log('\nPolling for task status...');
    const finalStatus = await client.tasks.pollUntilComplete(taskId, {
      pollingInterval: 2000,
      timeout: 60000,
    });

    console.log('\nFinal task status:', finalStatus.status);

    // Step 6: Check connection health after operations
    const postOpStatus = client.connection.getStatus();
    console.log('\nPost-operation connection status:', postOpStatus);

    // Step 7: Gracefully disconnect
    console.log('\nDisconnecting...');
    await client.connection.disconnect();
    console.log('Disconnected successfully');

  } catch (error) {
    console.error('Error:', error);
    
    // Always try to disconnect on error
    try {
      await client.connection.disconnect();
    } catch (disconnectError) {
      console.error('Error during disconnect:', disconnectError);
    }
  }
}

/**
 * Example showing connection resilience and reconnection
 */
async function connectionResilienceExample() {
  const client = new ConduitCoreClient({
    apiKey: process.env.CONDUIT_API_KEY || 'your-api-key',
    baseURL: process.env.CONDUIT_API_URL || 'http://localhost:5000',
    signalR: {
      reconnectDelay: [0, 1000, 2000, 5000, 10000], // Aggressive reconnect
      connectionTimeout: 15000,
    }
  });

  try {
    // Monitor connection state changes (when implemented)
    // client.connection.onConnectionStateChange((hub, state) => {
    //   console.log(`Connection state changed: ${hub} -> ${state}`);
    // });

    // Initial connection check
    console.log('Initial connection status:', client.connection.getStatus());

    // Simulate network issues by updating configuration
    console.log('\nSimulating configuration change...');
    await client.connection.updateConfiguration({
      connectionTimeout: 5000, // Reduce timeout
    });

    // Check if still connected after config change
    if (!client.connection.isConnected()) {
      console.log('Connection lost during config update, reconnecting...');
      await client.connection.reconnect();
    }

    // Verify reconnection
    const reconnected = await client.connection.waitForConnection(10000);
    console.log('Reconnection successful:', reconnected);

  } finally {
    await client.connection.disconnect();
  }
}

// Run examples
if (require.main === module) {
  console.log('=== Connection Integration Example ===\n');
  connectionIntegrationExample()
    .then(() => {
      console.log('\n\n=== Connection Resilience Example ===\n');
      return connectionResilienceExample();
    })
    .catch(console.error);
}