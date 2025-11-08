# SignalR Getting Started Guide

A beginner-friendly guide to get you connected and using Conduit's real-time features quickly.

## Prerequisites

Before you start, make sure you have:

1. **A valid Virtual Key** - Your Conduit virtual key (format: `condt_...`)
2. **API Access** - Ensure your virtual key has appropriate permissions
3. **Development Environment** - JavaScript/TypeScript, Python, or C#/.NET environment
4. **SignalR Client Library** - Install the appropriate client for your platform

### Install SignalR Client

**JavaScript/TypeScript:**
```bash
npm install @microsoft/signalr
```

**Python:**
```bash
pip install websockets aiohttp
```

**C#/.NET:**
```bash
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

## Quick Start: Your First Connection

### Step 1: Choose Your Hub

Conduit has different hubs for different features:

| Feature | Hub Name | Use Case |
|---------|----------|----------|
| `navigation-state` | Real-time UI navigation updates | WebUI state synchronization |
| `image-generation` | Image creation progress | Track DALL-E, Stable Diffusion tasks |
| `video-generation` | Video creation progress | Track video generation tasks |
| `tasks` | General async operations | Any long-running operation |

For this guide, we'll use the **navigation-state** hub as it's the simplest to get started with.

### Step 2: Basic Connection (JavaScript)

```javascript
// Basic connection example
import * as signalR from '@microsoft/signalr';

const virtualKey = 'condt_your_virtual_key_here';
const hubUrl = 'https://api.conduit.im/hubs/navigation-state';

// Create connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
        accessTokenFactory: () => virtualKey,
        withCredentials: false
    })
    .withAutomaticReconnect()
    .build();

// Connect
async function connect() {
    try {
        await connection.start();
        console.log('✅ Connected to navigation-state hub');
    } catch (err) {
        console.error('❌ Connection failed:', err);
    }
}

connect();
```

### Step 3: Listen for Events

```javascript
// Listen for navigation state updates
connection.on('NavigationStateUpdated', (data) => {
    console.log('📡 Navigation update received:', data);
});

// Listen for connection status
connection.onreconnecting(() => {
    console.log('🔄 Reconnecting...');
});

connection.onreconnected(() => {
    console.log('✅ Reconnected!');
});

connection.onclose((error) => {
    if (error) {
        console.error('❌ Connection closed with error:', error);
    } else {
        console.log('👋 Connection closed');
    }
});
```

### Step 4: Test Your Connection

Run your code and you should see:
```
✅ Connected to navigation-state hub
```

If you're using the WebUI, navigate around and you should see navigation updates in your console.

## Working with Task-Based Hubs

For more advanced use cases like image generation, here's how to work with task-based hubs:

### Image Generation Example

```javascript
// Connect to image generation hub
const imageConnection = new signalR.HubConnectionBuilder()
    .withUrl('https://api.conduit.im/hubs/image-generation', {
        accessTokenFactory: () => virtualKey
    })
    .withAutomaticReconnect()
    .build();

await imageConnection.start();

// Subscribe to a specific task
const taskId = 'your-task-id-here';
await imageConnection.invoke('SubscribeToTask', taskId);

// Listen for progress updates
imageConnection.on('TaskProgress', (taskId, progress) => {
    console.log(`🎨 Task ${taskId}: ${progress}% complete`);
});

// Listen for completion
imageConnection.on('TaskCompleted', (taskId, result) => {
    console.log(`✅ Task ${taskId} completed!`);
    console.log(`🖼️ Image URL: ${result.image_url}`);
});

// Listen for failures
imageConnection.on('TaskFailed', (taskId, error) => {
    console.error(`❌ Task ${taskId} failed: ${error}`);
});
```

### Creating and Tracking a Task

```javascript
// First, create an image generation task via REST API
async function generateImage() {
    const response = await fetch('https://api.conduit.im/v1/images/generations/async', {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${virtualKey}`,
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            model: 'dall-e-3',
            prompt: 'A serene mountain landscape at sunset',
            size: '1024x1024'
        })
    });

    const result = await response.json();
    const taskId = result.task_id;

    // Subscribe to real-time updates
    await imageConnection.invoke('SubscribeToTask', taskId);
    
    console.log(`🚀 Started image generation: ${taskId}`);
    return taskId;
}

// Use it
generateImage().catch(console.error);
```

## Error Handling Best Practices

### Connection Errors

```javascript
// Robust error handling
connection.onclose(async (error) => {
    if (error) {
        console.error('Connection lost:', error);
        
        // Manual reconnection with exponential backoff
        let retryCount = 0;
        const maxRetries = 5;
        
        while (retryCount < maxRetries) {
            try {
                const delay = Math.min(1000 * Math.pow(2, retryCount), 30000);
                console.log(`Retrying in ${delay}ms... (attempt ${retryCount + 1})`);
                
                await new Promise(resolve => setTimeout(resolve, delay));
                await connection.start();
                
                console.log('✅ Reconnected successfully');
                break;
            } catch (retryError) {
                retryCount++;
                console.error(`Retry ${retryCount} failed:`, retryError);
                
                if (retryCount >= maxRetries) {
                    console.error('❌ Max retries reached. Manual intervention required.');
                }
            }
        }
    }
});
```

### Hub Method Errors

```javascript
// Handle hub method call errors
try {
    await connection.invoke('SubscribeToTask', taskId);
} catch (error) {
    if (error.message.includes('already subscribed')) {
        console.log('ℹ️ Already subscribed to this task');
    } else if (error.message.includes('unauthorized')) {
        console.error('🔒 Authentication failed - check your virtual key');
    } else {
        console.error('❌ Hub method failed:', error);
    }
}
```

## Connection Status Management

### Track Connection State

```javascript
class ConnectionManager {
    constructor(hubUrl, virtualKey) {
        this.hubUrl = hubUrl;
        this.virtualKey = virtualKey;
        this.connection = null;
        this.isConnected = false;
        this.subscribers = new Set();
    }

    async connect() {
        if (this.connection) {
            await this.connection.stop();
        }

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl, {
                accessTokenFactory: () => this.virtualKey
            })
            .withAutomaticReconnect()
            .build();

        this.setupEventHandlers();
        
        await this.connection.start();
        this.isConnected = true;
    }

    setupEventHandlers() {
        this.connection.onreconnected(() => {
            this.isConnected = true;
            this.notifySubscribers('connected');
        });

        this.connection.onclose(() => {
            this.isConnected = false;
            this.notifySubscribers('disconnected');
        });
    }

    onConnectionChange(callback) {
        this.subscribers.add(callback);
        
        // Return unsubscribe function
        return () => this.subscribers.delete(callback);
    }

    notifySubscribers(status) {
        this.subscribers.forEach(callback => callback(status));
    }

    async invoke(method, ...args) {
        if (!this.isConnected) {
            throw new Error('Not connected to hub');
        }
        return await this.connection.invoke(method, ...args);
    }
}

// Usage
const manager = new ConnectionManager(hubUrl, virtualKey);

// Subscribe to connection changes
const unsubscribe = manager.onConnectionChange((status) => {
    console.log(`Connection status: ${status}`);
    document.getElementById('status').textContent = status;
});

await manager.connect();
```

## Common Pitfalls and Solutions

### ❌ Mistake: Multiple Connections to Same Hub

```javascript
// DON'T do this
const conn1 = new signalR.HubConnectionBuilder().withUrl(hubUrl).build();
const conn2 = new signalR.HubConnectionBuilder().withUrl(hubUrl).build();
await conn1.start();
await conn2.start(); // Wastes resources
```

```javascript
// ✅ DO this instead
const connection = new signalR.HubConnectionBuilder().withUrl(hubUrl).build();
await connection.start();
// Reuse the same connection for multiple subscriptions
```

### ❌ Mistake: Forgetting to Unsubscribe

```javascript
// DON'T do this
await connection.invoke('SubscribeToTask', taskId);
// Never unsubscribes...
```

```javascript
// ✅ DO this instead
await connection.invoke('SubscribeToTask', taskId);

// Later, when done
await connection.invoke('UnsubscribeFromTask', taskId);

// Or use a cleanup function
window.addEventListener('beforeunload', async () => {
    await connection.invoke('UnsubscribeFromTask', taskId);
});
```

### ❌ Mistake: Wrong Authentication

```javascript
// DON'T use virtual key for admin hubs
const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://admin-api.conduit.im/hubs/admin-notifications', {
        accessTokenFactory: () => virtualKey // ❌ Wrong!
    })
    .build();
```

```javascript
// ✅ Use master key for admin hubs
const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://admin-api.conduit.im/hubs/admin-notifications', {
        accessTokenFactory: () => masterKey // ✅ Correct!
    })
    .build();
```

## Testing Your Integration

### Simple Connection Test

```html
<!DOCTYPE html>
<html>
<head>
    <title>SignalR Test</title>
</head>
<body>
    <div id="status">Disconnected</div>
    <div id="messages"></div>

    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
    <script>
        const virtualKey = 'condt_your_key_here';
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('https://api.conduit.im/hubs/navigation-state', {
                accessTokenFactory: () => virtualKey
            })
            .build();

        function updateStatus(message) {
            document.getElementById('status').textContent = message;
        }

        function addMessage(message) {
            const div = document.createElement('div');
            div.textContent = `${new Date().toLocaleTimeString()}: ${message}`;
            document.getElementById('messages').appendChild(div);
        }

        connection.start()
            .then(() => {
                updateStatus('Connected ✅');
                addMessage('Connected to navigation-state hub');
            })
            .catch(err => {
                updateStatus('Failed ❌');
                addMessage(`Connection error: ${err}`);
            });

        connection.on('NavigationStateUpdated', (data) => {
            addMessage(`Navigation update: ${JSON.stringify(data)}`);
        });
    </script>
</body>
</html>
```

### Debug Console Commands

Open your browser's developer console and try these commands:

```javascript
// Check connection state
console.log('Connection state:', connection.state);

// Test hub method
connection.invoke('GetConnectionInfo')
    .then(info => console.log('Connection info:', info))
    .catch(err => console.error('Method failed:', err));

// Enable detailed logging
const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => virtualKey })
    .configureLogging(signalR.LogLevel.Debug) // Add this line
    .build();
```

## Next Steps

Now that you have a basic connection working, explore these advanced features:

### 1. **Multiple Hub Connections**
Learn to connect to multiple hubs simultaneously for complex applications.

### 2. **Event-Driven Architecture**
Integrate SignalR events with your application's state management (Redux, Vuex, etc.).

### 3. **Production Considerations**
- Implement proper error boundaries
- Add monitoring and alerting
- Configure load balancing for multiple instances

### 4. **Advanced Authentication**
- Token refresh mechanisms
- Role-based hub access
- Custom authentication flows

### 5. **Performance Optimization**
- Connection pooling
- Message compression
- Efficient event handling

## Troubleshooting

### Connection Issues

**Problem**: "Connection failed"
- ✅ Check your virtual key format (`condt_...`)
- ✅ Verify network connectivity to `api.conduit.im`
- ✅ Ensure CORS is properly configured
- ✅ Check browser console for detailed errors

**Problem**: "Authentication failed"
- ✅ Verify virtual key permissions
- ✅ Check key expiration
- ✅ Ensure using correct authentication scheme

**Problem**: "Hub not found"
- ✅ Verify hub URL spelling
- ✅ Check if hub exists for your plan
- ✅ Confirm API version compatibility

### Event Issues

**Problem**: "Not receiving events"
- ✅ Verify subscription was successful
- ✅ Check event name spelling (case-sensitive)
- ✅ Ensure proper group membership
- ✅ Confirm task ownership for task-based events

**Problem**: "Events received multiple times"
- ✅ Check for duplicate subscriptions
- ✅ Ensure proper cleanup on reconnection
- ✅ Verify event handler registration

## Getting Help

- **Documentation**: Check the [Hub Reference Guide](../hub-reference.md)
- **Examples**: See [Real-Time Client Examples](../../Real-Time-Client-Examples.md)
- **Quick Reference**: Use the [SignalR Quick Reference](../../SignalR-Quick-Reference.md)
- **Architecture**: Review [SignalR Architecture](../architecture.md)

## Sample Code Repository

For complete working examples in multiple languages, check out our sample repository:

```bash
git clone https://github.com/knnlabs/conduit-signalr-examples
cd conduit-signalr-examples
```

The repository includes:
- Complete JavaScript/TypeScript examples
- Python async implementations
- C#/.NET samples
- React/Vue.js integrations
- Error handling patterns
- Production-ready configurations

Happy coding! 🚀