# Comprehensive SignalR Redis Backplane Testing Guide

This guide provides detailed instructions for testing the SignalR Redis backplane implementation to ensure it works correctly across multiple Core API instances.

## Table of Contents
1. [Environment Setup](#environment-setup)
2. [Testing Tools](#testing-tools)
3. [Test Scenarios](#test-scenarios)
4. [Automated Testing](#automated-testing)
5. [Performance Testing](#performance-testing)
6. [Troubleshooting](#troubleshooting)

## Environment Setup

### Option 1: Docker Compose Setup (Recommended)

Create a `docker-compose.test.yml` file:

```yaml
version: '3.8'

services:
  redis-signalr:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-signalr-data:/data
    command: redis-server --appendonly yes

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: conduit
      POSTGRES_USER: conduit
      POSTGRES_PASSWORD: conduit123
    ports:
      - "5432:5432"

  api-1:
    build:
      context: .
      dockerfile: ConduitLLM.Http/Dockerfile
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__Database=Host=postgres;Database=conduit;Username=conduit;Password=conduit123
      - ConnectionStrings__RedisSignalR=redis-signalr:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
      - CONDUIT_SKIP_DATABASE_INIT=true
    ports:
      - "5000:5000"
    depends_on:
      - postgres
      - redis-signalr

  api-2:
    build:
      context: .
      dockerfile: ConduitLLM.Http/Dockerfile
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__Database=Host=postgres;Database=conduit;Username=conduit;Password=conduit123
      - ConnectionStrings__RedisSignalR=redis-signalr:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
      - CONDUIT_SKIP_DATABASE_INIT=true
    ports:
      - "5001:5000"
    depends_on:
      - postgres
      - redis-signalr

  api-3:
    build:
      context: .
      dockerfile: ConduitLLM.Http/Dockerfile
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__Database=Host=postgres;Database=conduit;Username=conduit;Password=conduit123
      - ConnectionStrings__RedisSignalR=redis-signalr:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
      - CONDUIT_SKIP_DATABASE_INIT=true
    ports:
      - "5002:5000"
    depends_on:
      - postgres
      - redis-signalr

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./nginx-test.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - api-1
      - api-2
      - api-3

volumes:
  redis-signalr-data:
```

Create `nginx-test.conf`:

```nginx
events {
    worker_connections 1024;
}

http {
    upstream conduit_api {
        server api-1:5000;
        server api-2:5000;
        server api-3:5000;
    }

    server {
        listen 80;

        location / {
            proxy_pass http://conduit_api;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

### Option 2: Local Development Setup

```bash
# Terminal 1: Start Redis
docker run -d --name redis-signalr -p 6379:6379 redis:7-alpine

# Terminal 2: Start PostgreSQL
docker run -d --name postgres-test -p 5432:5432 \
  -e POSTGRES_DB=conduit \
  -e POSTGRES_USER=conduit \
  -e POSTGRES_PASSWORD=conduit123 \
  postgres:15

# Wait for services to start
sleep 5

# Terminal 3: First API instance
export ASPNETCORE_URLS=http://localhost:5000
export ConnectionStrings__Database="Host=localhost;Database=conduit;Username=conduit;Password=conduit123"
export ConnectionStrings__RedisSignalR="localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
export CONDUIT_SKIP_DATABASE_INIT=false  # Only for first instance
dotnet run --project ConduitLLM.Http

# Terminal 4: Second API instance
export ASPNETCORE_URLS=http://localhost:5001
export ConnectionStrings__Database="Host=localhost;Database=conduit;Username=conduit;Password=conduit123"
export ConnectionStrings__RedisSignalR="localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
export CONDUIT_SKIP_DATABASE_INIT=true
dotnet run --project ConduitLLM.Http

# Terminal 5: Third API instance
export ASPNETCORE_URLS=http://localhost:5002
export ConnectionStrings__Database="Host=localhost;Database=conduit;Username=conduit;Password=conduit123"
export ConnectionStrings__RedisSignalR="localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
export CONDUIT_SKIP_DATABASE_INIT=true
dotnet run --project ConduitLLM.Http
```

## Testing Tools

### 1. SignalR Test Client (HTML/JavaScript)

Create `signalr-test-client.html`:

```html
<!DOCTYPE html>
<html>
<head>
    <title>SignalR Multi-Instance Test</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.0/signalr.min.js"></script>
    <style>
        .instance { 
            border: 1px solid #ccc; 
            padding: 10px; 
            margin: 10px; 
            float: left; 
            width: 30%; 
        }
        .connected { background-color: #90EE90; }
        .disconnected { background-color: #FFB6C1; }
        .message { 
            margin: 5px 0; 
            padding: 5px; 
            background-color: #f0f0f0; 
            font-size: 12px;
        }
    </style>
</head>
<body>
    <h1>SignalR Redis Backplane Test</h1>
    
    <div id="instance1" class="instance disconnected">
        <h3>Instance 1 (Port 5000)</h3>
        <div class="status">Disconnected</div>
        <div class="messages"></div>
    </div>
    
    <div id="instance2" class="instance disconnected">
        <h3>Instance 2 (Port 5001)</h3>
        <div class="status">Disconnected</div>
        <div class="messages"></div>
    </div>
    
    <div id="instance3" class="instance disconnected">
        <h3>Instance 3 (Port 5002)</h3>
        <div class="status">Disconnected</div>
        <div class="messages"></div>
    </div>
    
    <div style="clear: both;"></div>
    
    <button onclick="testBroadcast()">Test Broadcast</button>
    <button onclick="reconnectAll()">Reconnect All</button>

    <script>
        const instances = [
            { port: 5000, connection: null, element: 'instance1' },
            { port: 5001, connection: null, element: 'instance2' },
            { port: 5002, connection: null, element: 'instance3' }
        ];

        async function connectInstance(instance) {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl(`http://localhost:${instance.port}/hubs/navigation-state`)
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            connection.on("NavigationStateChanged", (state) => {
                addMessage(instance.element, `Navigation state changed: ${JSON.stringify(state)}`);
            });

            connection.on("ModelMappingChanged", (mapping) => {
                addMessage(instance.element, `Model mapping changed: ${JSON.stringify(mapping)}`);
            });

            connection.onreconnecting(() => {
                updateStatus(instance.element, 'Reconnecting...', false);
            });

            connection.onreconnected(() => {
                updateStatus(instance.element, 'Connected', true);
            });

            connection.onclose(() => {
                updateStatus(instance.element, 'Disconnected', false);
            });

            try {
                await connection.start();
                updateStatus(instance.element, 'Connected', true);
                await connection.invoke("RequestCurrentState");
                instance.connection = connection;
            } catch (err) {
                console.error('Error connecting to instance', instance.port, err);
                updateStatus(instance.element, 'Connection Failed', false);
            }
        }

        function updateStatus(elementId, status, connected) {
            const element = document.getElementById(elementId);
            element.className = `instance ${connected ? 'connected' : 'disconnected'}`;
            element.querySelector('.status').textContent = status;
        }

        function addMessage(elementId, message) {
            const messagesDiv = document.getElementById(elementId).querySelector('.messages');
            const messageDiv = document.createElement('div');
            messageDiv.className = 'message';
            messageDiv.textContent = `${new Date().toLocaleTimeString()}: ${message}`;
            messagesDiv.insertBefore(messageDiv, messagesDiv.firstChild);
            
            // Keep only last 10 messages
            while (messagesDiv.children.length > 10) {
                messagesDiv.removeChild(messagesDiv.lastChild);
            }
        }

        async function testBroadcast() {
            // This would typically be done via Admin API
            // For testing, you can trigger an event through one instance
            if (instances[0].connection) {
                console.log('Triggering test broadcast...');
                // Make an API call to trigger an event
                fetch('http://localhost:5000/api/test/trigger-navigation-update', {
                    method: 'POST'
                });
            }
        }

        async function reconnectAll() {
            for (const instance of instances) {
                if (instance.connection) {
                    await instance.connection.stop();
                }
                await connectInstance(instance);
            }
        }

        // Connect to all instances on load
        window.onload = async () => {
            for (const instance of instances) {
                await connectInstance(instance);
            }
        };
    </script>
</body>
</html>
```

### 2. PowerShell Test Script

Create `Test-SignalRBackplane.ps1`:

```powershell
# Test-SignalRBackplane.ps1
param(
    [string[]]$ApiUrls = @("http://localhost:5000", "http://localhost:5001", "http://localhost:5002"),
    [string]$VirtualKey = "test-key-123",
    [int]$TestDurationSeconds = 30
)

Write-Host "SignalR Redis Backplane Test Script" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green

# Function to create SignalR connection
function New-SignalRConnection {
    param([string]$BaseUrl)
    
    $connection = [Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder]::new()
    $connection = $connection.WithUrl("$BaseUrl/hubs/navigation-state")
    $connection = $connection.Build()
    
    return $connection
}

# Test 1: Verify all instances are running
Write-Host "`nTest 1: Verifying API instances..." -ForegroundColor Yellow
foreach ($url in $ApiUrls) {
    try {
        $response = Invoke-RestMethod -Uri "$url/health" -Method Get
        Write-Host "✓ $url is healthy" -ForegroundColor Green
    } catch {
        Write-Host "✗ $url is not responding" -ForegroundColor Red
    }
}

# Test 2: Redis connectivity
Write-Host "`nTest 2: Checking Redis connectivity..." -ForegroundColor Yellow
docker exec redis-signalr redis-cli ping | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Redis is responding" -ForegroundColor Green
} else {
    Write-Host "✗ Redis is not responding" -ForegroundColor Red
    exit 1
}

# Test 3: Monitor Redis channels
Write-Host "`nTest 3: Monitoring Redis SignalR channels..." -ForegroundColor Yellow
$redisMonitor = Start-Job -ScriptBlock {
    docker exec redis-signalr redis-cli MONITOR | Select-String "conduit_signalr"
}

# Test 4: SignalR message propagation
Write-Host "`nTest 4: Testing SignalR message propagation..." -ForegroundColor Yellow
Write-Host "Creating model mapping via Admin API..."

# Create a test model mapping
$modelMapping = @{
    virtualModelName = "test-model-$(Get-Random)"
    providerModelName = "gpt-3.5-turbo"
    providerName = "openai"
} | ConvertTo-Json

try {
    # Post to first instance
    Invoke-RestMethod -Uri "$($ApiUrls[0])/api/admin/model-mappings" `
        -Method Post `
        -Body $modelMapping `
        -ContentType "application/json" `
        -Headers @{ "Authorization" = "Bearer admin-key" }
    
    Write-Host "✓ Model mapping created" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to create model mapping: $_" -ForegroundColor Red
}

# Wait and check Redis monitor
Start-Sleep -Seconds 5
$redisOutput = Receive-Job -Job $redisMonitor
if ($redisOutput) {
    Write-Host "✓ Redis SignalR messages detected:" -ForegroundColor Green
    $redisOutput | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "✗ No Redis SignalR messages detected" -ForegroundColor Red
}

Stop-Job -Job $redisMonitor
Remove-Job -Job $redisMonitor

Write-Host "`nTest completed!" -ForegroundColor Green
```

### 3. cURL Test Commands

```bash
# Test health endpoints
for port in 5000 5001 5002; do
    echo "Testing instance on port $port:"
    curl -s http://localhost:$port/health | jq .
done

# Monitor Redis in real-time
redis-cli MONITOR | grep "conduit_signalr"

# Subscribe to SignalR channels
redis-cli PSUBSCRIBE "conduit_signalr:*"

# Check Redis database 2 (SignalR)
redis-cli -n 2 DBSIZE

# Test creating an event that should propagate
curl -X POST http://localhost:5000/api/admin/model-mappings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-admin-key" \
  -d '{
    "virtualModelName": "test-model-'$(date +%s)'",
    "providerModelName": "gpt-3.5-turbo",
    "providerName": "openai"
  }'
```

## Test Scenarios

### Scenario 1: Basic Message Propagation

1. Open the HTML test client in 3 browser tabs
2. Each tab connects to a different instance
3. Trigger an event via Admin API
4. Verify all 3 clients receive the update

### Scenario 2: Instance Failure Recovery

1. Connect clients to all instances
2. Stop one instance: `docker stop conduit_api-2_1`
3. Verify clients reconnect when instance restarts
4. Verify no messages lost during downtime

### Scenario 3: Redis Failure Recovery

1. Connect clients to all instances
2. Stop Redis: `docker stop redis-signalr`
3. Observe SignalR fallback behavior
4. Restart Redis: `docker start redis-signalr`
5. Verify SignalR reconnects to Redis

### Scenario 4: Load Testing

```bash
# Use SignalR load testing tool
npm install -g @microsoft/signalr-test-tool

# Run load test with 1000 clients
signalr-test-tool \
  --url http://localhost/hubs/navigation-state \
  --connections 1000 \
  --duration 300 \
  --interval 100
```

## Automated Testing

### Integration Test Example

Create `SignalRBackplaneTests.cs`:

```csharp
[TestClass]
public class SignalRBackplaneTests
{
    private List<WebApplication> _apiInstances;
    private IConnectionMultiplexer _redis;

    [TestInitialize]
    public async Task Setup()
    {
        // Start Redis container
        await StartRedisContainer();
        
        // Start multiple API instances
        _apiInstances = new List<WebApplication>();
        for (int i = 0; i < 3; i++)
        {
            var app = CreateApiInstance(5000 + i);
            await app.StartAsync();
            _apiInstances.Add(app);
        }
        
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
    }

    [TestMethod]
    public async Task SignalR_MessagesPropagateAcrossInstances()
    {
        // Arrange
        var connections = new List<HubConnection>();
        var receivedMessages = new ConcurrentBag<string>();
        
        // Connect to each instance
        for (int i = 0; i < 3; i++)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"http://localhost:{5000 + i}/hubs/navigation-state")
                .Build();
                
            connection.On<object>("NavigationStateChanged", (state) =>
            {
                receivedMessages.Add($"Instance{i}: {JsonSerializer.Serialize(state)}");
            });
            
            await connection.StartAsync();
            connections.Add(connection);
        }
        
        // Act - Trigger event via first instance
        var client = new HttpClient();
        await client.PostAsJsonAsync(
            "http://localhost:5000/api/admin/trigger-navigation-update",
            new { test = true });
        
        // Wait for propagation
        await Task.Delay(1000);
        
        // Assert
        Assert.AreEqual(3, receivedMessages.Count, 
            "All instances should receive the message");
        
        // Cleanup
        foreach (var conn in connections)
        {
            await conn.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task SignalR_UsesRedisBackplane()
    {
        // Arrange
        var subscriber = _redis.GetSubscriber();
        var messagesReceived = new List<string>();
        
        // Subscribe to Redis SignalR channels
        await subscriber.SubscribeAsync("conduit_signalr:*", (channel, message) =>
        {
            messagesReceived.Add($"{channel}: {message}");
        });
        
        // Act - Connect and send message
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/hubs/navigation-state")
            .Build();
        await connection.StartAsync();
        
        // Trigger an update
        await TriggerNavigationUpdate();
        await Task.Delay(500);
        
        // Assert
        Assert.IsTrue(messagesReceived.Any(), 
            "Redis should receive SignalR messages");
        Assert.IsTrue(messagesReceived.Any(m => m.Contains("conduit_signalr:")), 
            "Messages should use correct channel prefix");
    }
}
```

## Performance Testing

### 1. Connection Scalability Test

```bash
#!/bin/bash
# test-connection-scale.sh

echo "SignalR Connection Scalability Test"
echo "==================================="

# Test with increasing number of connections
for connections in 10 100 500 1000 5000 10000; do
    echo -n "Testing with $connections connections... "
    
    # Use artillery for load testing
    cat > artillery-config.yml << EOF
config:
  target: "http://localhost"
  phases:
    - duration: 60
      arrivalRate: $(($connections / 60))
  processor: "./signalr-processor.js"

scenarios:
  - name: "SignalR Connection Test"
    engine: "ws"
    flow:
      - connect:
          url: "/hubs/navigation-state"
      - think: 30
      - disconnect
EOF

    artillery run artillery-config.yml > "results-$connections.txt" 2>&1
    
    # Extract metrics
    success_rate=$(grep "Successful connections" "results-$connections.txt" | awk '{print $3}')
    avg_latency=$(grep "Mean latency" "results-$connections.txt" | awk '{print $3}')
    
    echo "Success: $success_rate%, Latency: ${avg_latency}ms"
done
```

### 2. Message Throughput Test

```python
# test_message_throughput.py
import asyncio
import time
import statistics
from signalr_client_aio import SignalRClient

async def test_message_throughput(num_clients=100, duration_seconds=60):
    clients = []
    message_latencies = []
    
    # Create clients
    for i in range(num_clients):
        client = SignalRClient(f"http://localhost:{5000 + (i % 3)}/hubs/navigation-state")
        
        def on_message(data):
            latency = time.time() - data.get('timestamp', time.time())
            message_latencies.append(latency * 1000)  # Convert to ms
        
        client.on('NavigationStateChanged', on_message)
        await client.start()
        clients.append(client)
    
    print(f"Connected {num_clients} clients")
    
    # Send messages for duration
    start_time = time.time()
    messages_sent = 0
    
    while time.time() - start_time < duration_seconds:
        # Trigger message broadcast
        await trigger_navigation_update()
        messages_sent += 1
        await asyncio.sleep(0.1)  # 10 messages per second
    
    # Calculate metrics
    print(f"\nTest Results:")
    print(f"Messages sent: {messages_sent}")
    print(f"Total messages received: {len(message_latencies)}")
    print(f"Average latency: {statistics.mean(message_latencies):.2f}ms")
    print(f"P95 latency: {statistics.quantiles(message_latencies, n=20)[18]:.2f}ms")
    print(f"P99 latency: {statistics.quantiles(message_latencies, n=100)[98]:.2f}ms")
    
    # Cleanup
    for client in clients:
        await client.stop()

if __name__ == "__main__":
    asyncio.run(test_message_throughput())
```

## Troubleshooting

### Common Issues and Solutions

1. **SignalR not using Redis backplane**
   ```bash
   # Check startup logs
   docker logs conduit_api-1_1 | grep "SignalR configured"
   
   # Should see: "[Conduit] SignalR configured with Redis backplane for horizontal scaling"
   ```

2. **Messages not propagating between instances**
   ```bash
   # Check Redis connectivity from each container
   docker exec conduit_api-1_1 redis-cli -h redis-signalr ping
   
   # Monitor Redis for SignalR activity
   docker exec redis-signalr redis-cli MONITOR | grep conduit_signalr
   ```

3. **High message latency**
   ```bash
   # Check Redis latency
   docker exec redis-signalr redis-cli --latency-history
   
   # Check Redis memory usage
   docker exec redis-signalr redis-cli INFO memory
   ```

4. **Connection drops**
   ```bash
   # Check container logs for errors
   docker logs conduit_api-1_1 --tail 100 | grep -E "(SignalR|Redis|Error)"
   
   # Check network connectivity
   docker exec conduit_api-1_1 ping redis-signalr
   ```

### Debug Commands

```bash
# View Redis SignalR keys
redis-cli -n 2 KEYS "*"

# Monitor specific SignalR channel
redis-cli SUBSCRIBE "conduit_signalr:ack:*"

# Check Redis connection pool stats
redis-cli CLIENT LIST | grep conduit

# View SignalR metrics (if Application Insights configured)
curl http://localhost:5000/metrics | grep signalr
```

## Success Criteria

Your SignalR Redis backplane is working correctly when:

1. ✅ All API instances show: `[Conduit] SignalR configured with Redis backplane for horizontal scaling`
2. ✅ Clients connected to different instances receive the same updates
3. ✅ Redis MONITOR shows messages with `conduit_signalr:` prefix
4. ✅ Message propagation latency < 50ms
5. ✅ No message loss during instance scaling (up or down)
6. ✅ System handles 10,000 concurrent connections
7. ✅ Redis memory usage remains stable under load
8. ✅ Clients automatically reconnect after instance failures

## Next Steps

1. Set up monitoring dashboards for SignalR metrics
2. Configure alerts for high latency or connection failures
3. Implement SignalR authentication for production
4. Consider Redis Sentinel or Cluster for high availability
5. Test with your actual load patterns and adjust configuration