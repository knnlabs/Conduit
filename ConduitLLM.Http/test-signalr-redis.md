# Testing SignalR Redis Backplane

This document provides instructions for testing the SignalR Redis backplane implementation.

## Prerequisites

1. Redis instance running and accessible
2. Multiple Core API instances running
3. WebUI or test client with SignalR connection

## Test Scenarios

### 1. Single Instance Test (Baseline)

```bash
# Start Redis
docker run -d --name redis-signalr -p 6379:6379 redis:latest

# Configure and start Core API
export ConnectionStrings__RedisSignalR=localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
dotnet run --project ConduitLLM.Http

# Verify log output shows:
# [Conduit] SignalR configured with Redis backplane for horizontal scaling
```

### 2. Multi-Instance Test

```bash
# Terminal 1 - Start first instance on port 5000
export ASPNETCORE_URLS=http://localhost:5000
export ConnectionStrings__RedisSignalR=localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
dotnet run --project ConduitLLM.Http

# Terminal 2 - Start second instance on port 5001
export ASPNETCORE_URLS=http://localhost:5001
export ConnectionStrings__RedisSignalR=localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000
dotnet run --project ConduitLLM.Http

# Terminal 3 - Monitor Redis
redis-cli
> MONITOR
# Watch for messages with "conduit_signalr:" prefix
```

### 3. Client Connection Test

1. Connect WebUI or test client to instance 1 (port 5000)
2. Connect another client to instance 2 (port 5001)
3. Trigger an event (e.g., update model mapping via Admin API)
4. Verify both clients receive the update

### 4. Redis Channel Verification

```bash
# Subscribe to SignalR channels
redis-cli
> PSUBSCRIBE conduit_signalr:*

# In another terminal, check active channels
redis-cli
> PUBSUB CHANNELS conduit_signalr:*
```

## Expected Results

- ✅ Clients connected to different instances receive same updates
- ✅ Redis MONITOR shows SignalR messages with `conduit_signalr:` prefix
- ✅ No message loss during instance scaling
- ✅ Latency < 50ms for message propagation

## Troubleshooting

### SignalR Not Using Redis
- Check ConnectionStrings__RedisSignalR environment variable
- Verify Redis connectivity: `redis-cli ping`
- Check Core API logs for backplane initialization

### Messages Not Propagating
- Verify Redis database 2 is being used: `redis-cli -n 2`
- Check for firewall/network issues between instances
- Monitor Redis memory usage: `redis-cli INFO memory`

### High Latency
- Check Redis performance: `redis-cli --latency`
- Verify network latency between instances
- Consider dedicated Redis instance for SignalR