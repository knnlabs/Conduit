# RabbitMQ High-Throughput Configuration

As of the latest update, Conduit now supports RabbitMQ for horizontal scaling with production-ready configuration optimized for 1,000+ async tasks per minute.

## Overview

1. **Automatic Transport Detection**: The system automatically uses RabbitMQ when configured via environment variables
2. **Partitioned Processing**: Events are routed to partition queues based on entity IDs for ordered processing
3. **Durable Messaging**: All queues and messages are durable by default
4. **Health Monitoring**: Comprehensive RabbitMQ health checks monitor queue depths, memory usage, and performance

## High-Throughput Configuration (v2.0+)

**Optimized Settings for 1,000 tasks/minute:**
```bash
# Core configuration
CONDUITLLM__RABBITMQ__PREFETCHCOUNT=25        # Balanced for memory safety
CONDUITLLM__RABBITMQ__PARTITIONCOUNT=30       # 30 parallel processing streams
CONDUITLLM__RABBITMQ__CONCURRENTMESSAGELIMIT=50  # Prevents thread exhaustion

# Connection pooling
CONDUITLLM__RABBITMQ__MAXCONNECTIONS=5
CONDUITLLM__RABBITMQ__MINCONNECTIONS=2

# Advanced settings
CONDUITLLM__RABBITMQ__REQUESTEDHEARTBEAT=30
CONDUITLLM__RABBITMQ__PUBLISHERCONFIRMATION=true
```

## Key Features

- **Connection Pooling**: Reduces overhead and improves throughput
- **Circuit Breakers**: Prevents cascading failures (15-20% trip threshold)
- **Rate Limiting**: Protects consumers from overload (100 msg/sec for webhooks)
- **Batch Publishing**: Optimizes webhook delivery with 100-message batches
- **Queue Monitoring**: Alerts when queue depth exceeds 1,000 messages

## Queue-Specific Optimizations

- **Webhook Delivery**: PrefetchCount=100, ConcurrentLimit=75, Quorum queue
- **Video/Image Generation**: Standard prefetch with circuit breakers
- **Spend Updates**: PrefetchCount=10, ConcurrentLimit=1 for strict ordering

## Enabling RabbitMQ

To enable RabbitMQ, simply set the environment variables documented above. The system will:
- Switch from in-memory to RabbitMQ transport automatically
- Create necessary exchanges and queues on startup
- Route events based on partition keys for ordered processing
- Handle connection failures with automatic recovery
- Monitor performance and alert on degradation

**For detailed scaling procedures, see:** [RabbitMQ Scaling Guide](../RabbitMQ-Scaling-Guide.md)

## HTTP Client Connection Pooling for Webhook Delivery

Conduit implements optimized HTTP client connection pooling to support 1,000+ webhook deliveries per minute:

**Configuration:**
- **50 connections per server** - Supports 100 webhooks/second burst traffic
- **5-minute connection lifetime** - Balances reuse vs staleness
- **2-minute idle timeout** - Prevents resource waste
- **10-second request timeout** - Reduced from 30s for better scalability

**Resilience Features:**
- **Retry Policy**: 3 attempts with exponential backoff (2s, 4s, 8s)
- **Circuit Breaker**: Opens after 5 failures, 1-minute break duration
- **HTTP/2 Support**: Keep-alive pings every 30s with 20s timeout
- **Custom Timeouts**: Per-request timeout override support

**Monitoring:**
- Connection pool health check with 70%/90% warning/critical thresholds
- Prometheus metrics for requests, timeouts, duration, and active connections
- Detailed logging for slow requests (>5s) and failures

**See:** [HTTP Connection Pooling Guide](../HTTP-Connection-Pooling-Guide.md) for detailed configuration and troubleshooting.