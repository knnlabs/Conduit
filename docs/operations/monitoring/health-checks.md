# Health Monitoring and Alert System Guide

## Overview

Conduit's Health Monitoring system provides comprehensive real-time monitoring, alerting, and diagnostics for system health, performance, and security events. The system uses SignalR for real-time updates and supports multiple notification channels.

## Architecture

### Components

1. **Health Monitoring Service** - Core monitoring logic and health checks
2. **Alert Management Service** - Alert lifecycle management and suppression
3. **Performance Monitoring Service** - Tracks performance metrics and thresholds
4. **Security Event Monitoring** - Detects and alerts on security threats
5. **SignalR Hub** - Real-time alert streaming to connected clients
6. **Notification Channels** - Email, Webhook, and Slack notifications

### Alert Types

- **ServiceDown** - Critical service failures
- **ConnectivityIssue** - Network or connection problems
- **PerformanceDegradation** - Slow response times or high latency
- **ResourceExhaustion** - High CPU, memory, disk, or connection pool usage
- **SecurityEvent** - Authentication failures, brute force attempts, suspicious activity
- **ConfigurationError** - Invalid or missing configuration
- **Custom** - User-defined alerts

### Alert Severities

- **Critical** - Immediate action required, service impacting
- **Error** - Significant issue requiring attention
- **Warning** - Potential problem or degraded performance
- **Info** - Informational messages

## Configuration

### Health Monitoring Options

```json
{
  "HealthMonitoring": {
    "Enabled": true,
    "AlertRetentionDays": 7,
    "MaxActiveAlerts": 1000,
    "AlertCooldownMinutes": 5,
    "EnabledChannels": ["Email", "Webhook", "Slack"],
    "PerformanceThresholds": {
      "ApiResponseTimeMs": 5000,
      "DatabaseQueryTimeMs": 1000,
      "CacheHitRatePercent": 80,
      "ErrorRatePercent": 5
    },
    "ResourceThresholds": {
      "CpuUsagePercent": 80,
      "MemoryUsagePercent": 85,
      "DiskUsagePercent": 90,
      "ConnectionPoolUsagePercent": 80
    },
    "SecurityThresholds": {
      "AuthFailuresPerMinute": 10,
      "RateLimitViolationsPerMinute": 50,
      "DataTransferMB": 100
    }
  }
}
```

### Notification Channels

#### Email Configuration
```json
{
  "HealthMonitoring": {
    "EmailSettings": {
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "Username": "alerts@example.com",
      "Password": "secure-password",
      "FromAddress": "conduit-alerts@example.com",
      "ToAddresses": ["ops@example.com", "admin@example.com"]
    }
  }
}
```

#### Webhook Configuration
```json
{
  "HealthMonitoring": {
    "WebhookSettings": {
      "Url": "https://alerts.example.com/webhook",
      "Headers": {
        "Authorization": "Bearer webhook-token"
      },
      "IncludeDetails": true
    }
  }
}
```

#### Slack Configuration
```json
{
  "HealthMonitoring": {
    "SlackSettings": {
      "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
      "Channel": "#alerts",
      "Username": "Conduit Alerts",
      "IconEmoji": ":warning:"
    }
  }
}
```

## Using the Health Monitoring Dashboard

### Accessing the Dashboard

1. Navigate to `/health-monitoring` in the WebAdmin
2. The dashboard displays:
   - Overall system health status
   - Active alerts with severity indicators
   - Component health status
   - Performance metrics
   - Resource utilization graphs

### Managing Alerts

#### Acknowledging Alerts
1. Click on an active alert
2. Click "Acknowledge" button
3. Add notes about investigation/resolution
4. Alert remains active but marked as acknowledged

#### Resolving Alerts
1. Click on an active alert
2. Click "Resolve" button
3. Add resolution notes
4. Alert is moved to history

#### Creating Alert Suppressions
1. Go to Alert Management Panel
2. Navigate to "Suppressions" tab
3. Click "Create Suppression"
4. Configure:
   - Alert pattern (supports wildcards)
   - Component filter
   - Time range
   - Reason for suppression

### Real-Time Updates

The dashboard automatically updates via SignalR when:
- New alerts are triggered
- Alert states change
- System health status changes
- Performance metrics update

## Testing the System

### Using the Test Controller

The system includes a test controller for simulating various failure scenarios:

```bash
# Get available test scenarios
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/test/health-monitoring/scenarios

# Start a test scenario
curl -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/test/health-monitoring/start/service-down?durationSeconds=60

# Stop a test scenario
curl -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/test/health-monitoring/stop/service-down

# Trigger a custom alert
curl -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "severity": "Warning",
    "title": "Custom Test Alert",
    "message": "This is a test alert",
    "component": "TestComponent"
  }' \
  http://localhost:5000/api/test/health-monitoring/alert
```

### Available Test Scenarios

1. **service-down** - Simulates critical service unavailability
2. **high-cpu** - Creates high CPU usage
3. **memory-leak** - Simulates gradual memory exhaustion
4. **slow-response** - Generates slow API responses
5. **high-error-rate** - Produces increased API errors
6. **brute-force** - Simulates authentication attacks
7. **rate-limit-breach** - Generates excessive API usage
8. **data-exfiltration** - Simulates suspicious data transfers
9. **connection-pool** - Exhausts database connections
10. **disk-space** - Simulates low disk space

### Running the Test Script

```bash
# Run all tests
# Example: ./Scripts/test-health-monitoring.sh
# Note: This test script would need to be created

# Run 60-second demo with multiple scenarios
# Example: ./Scripts/test-health-monitoring.sh
# Note: This test script would need to be created demo
```

## Alert Rules

### Creating Alert Rules

Alert rules automatically trigger alerts based on conditions:

```csharp
var rule = new AlertRule
{
    Name = "High API Error Rate",
    Component = "API",
    AlertType = AlertType.PerformanceDegradation,
    Condition = "ErrorRate > 10%",
    Severity = AlertSeverity.Warning,
    IsEnabled = true,
    CooldownMinutes = 5
};
```

### Built-in Rules

1. **API Performance**
   - Response time > 5 seconds
   - Error rate > 5%
   - Request rate spike detection

2. **Resource Usage**
   - CPU > 80% for 5 minutes
   - Memory > 85%
   - Disk space < 10%
   - Connection pool > 80%

3. **Security**
   - 10+ auth failures/minute from same IP
   - 50+ rate limit violations/minute
   - Large data transfer patterns

## Monitoring Best Practices

### Alert Management

1. **Alert Fatigue Prevention**
   - Set appropriate thresholds
   - Use alert suppression during maintenance
   - Configure cooldown periods
   - Group related alerts

2. **Severity Guidelines**
   - **Critical**: Service down, data loss risk
   - **Error**: Degraded service, user impact
   - **Warning**: Potential issues, preventive action
   - **Info**: Notable events, no action required

3. **Response Procedures**
   - Document response steps in alert descriptions
   - Include suggested actions
   - Link to runbooks
   - Set up escalation paths

### Performance Optimization

1. **Metric Collection**
   - Use sampling for high-frequency metrics
   - Aggregate data before storage
   - Set retention policies
   - Monitor monitoring overhead

2. **Real-Time Updates**
   - Batch SignalR updates
   - Use throttling for rapid changes
   - Implement client-side buffering
   - Monitor WebSocket connections

## Troubleshooting

### Common Issues

1. **Alerts Not Triggering**
   - Check if monitoring is enabled
   - Verify threshold configuration
   - Check alert suppression rules
   - Review logs for errors

2. **Missing Real-Time Updates**
   - Verify SignalR connection
   - Check WebSocket support
   - Review firewall/proxy settings
   - Monitor hub connection state

3. **Notification Failures**
   - Verify channel configuration
   - Check network connectivity
   - Review authentication settings
   - Monitor retry attempts

### Diagnostic Commands

```bash
# Check health endpoint
curl http://localhost:5000/health

# Get current alerts
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/health-monitoring/alerts

# Check SignalR connectivity
wscat -c ws://localhost:5000/hubs/health-monitoring
```

## Integration with External Systems

### Prometheus Metrics

The system exposes metrics in Prometheus format:
- `conduit_alerts_total` - Total alerts by type and severity
- `conduit_alert_response_time` - Alert processing time
- `conduit_active_alerts` - Current active alert count

### Webhook Payload Format

```json
{
  "alertId": "guid",
  "severity": "Critical",
  "type": "ServiceDown",
  "component": "Database",
  "title": "Database Connection Failed",
  "message": "Cannot connect to primary database",
  "triggeredAt": "2024-01-20T10:30:00Z",
  "context": {
    "connectionString": "masked",
    "lastSuccess": "2024-01-20T10:25:00Z"
  },
  "suggestedActions": [
    "Check database server status",
    "Verify network connectivity"
  ]
}
```

## Security Considerations

1. **Access Control**
   - Test endpoints require admin authentication
   - Alert management requires appropriate permissions
   - Sensitive data is masked in alerts

2. **Data Protection**
   - Connection strings are sanitized
   - Passwords are never included in alerts
   - PII is excluded from alert context

3. **Audit Trail**
   - All alert actions are logged
   - User actions are tracked
   - Alert history is retained per policy

## External Health Monitoring Access

Health endpoints are protected from unauthorized external access while remaining accessible to:
- Internal/private network requests (Kubernetes probes, internal monitoring)
- External requests with a valid health monitoring key

### Configuration

For external monitoring services (BetterStack, Pingdom, UptimeRobot, etc.), configure the health monitoring key:

```bash
CONDUIT_HEALTH_MONITORING_KEY=<secure-random-key-32-chars-minimum>
```

Generate a secure key:
```bash
# Linux/macOS
openssl rand -base64 32

# PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

### Authentication

External requests must include the key in the `X-Conduit-Health-Key` header:

```bash
curl -H "X-Conduit-Health-Key: your-key-here" https://api.conduit.im/health
```

### Access Control Matrix

| Source | Authentication Required | Behavior |
|--------|------------------------|----------|
| Private network (10.x, 172.16-31.x, 192.168.x, 127.x) | None | Full access |
| External with valid key | `X-Conduit-Health-Key` header | Full access |
| External without key | N/A | `404 Not Found` |

> **Security Note:** Unauthorized external requests receive `404 Not Found` (not `401` or `403`) to hide the existence of health endpoints from potential attackers.

### BetterStack Configuration

1. Log in to BetterStack and create a new uptime monitor
2. Configure the monitor:
   - **URL**: `https://api.conduit.im/health`
   - **Check interval**: 30 seconds (recommended)
   - **Request method**: GET
3. Add custom header:
   - **Header name**: `X-Conduit-Health-Key`
   - **Header value**: Your configured key
4. Set expected response:
   - **Status code**: 200
   - **Response time warning**: 500ms
   - **Response time critical**: 2000ms

### Endpoints to Monitor

| Service | Endpoint | Purpose |
|---------|----------|---------|
| Gateway API | `https://api.conduit.im/health` | Basic Gateway liveness |
| Gateway API | `https://api.conduit.im/health/ready` | Gateway readiness (includes dependencies) |
| Admin API | `https://admin.conduit.im/health` | Basic Admin API liveness |
| Admin API | `https://admin.conduit.im/health/ready` | Admin API readiness |
| WebAdmin | `https://webadmin.conduit.im/api/health` | WebAdmin liveness |

### Detailed Health Endpoints

For internal monitoring dashboards, additional detailed endpoints are available:

- `/health/signalr` - SignalR connection statistics
- `/health/signalr/connections` - Active connection details
- `/health/signalr/queue` - Message queue statistics
- `/api/health/services` - Service health overview (Admin API)
- `/api/health/incidents` - Incident history (Admin API)
- `/api/health/history` - Health metrics history (Admin API)

These endpoints return the same `404 Not Found` for unauthorized external requests.

## Message Bus Health & Metrics

The event bus health check depends on the active messaging backend
(`ConduitLLM:Messaging:Backend`, epic #909):

| Backend | Check name | What it verifies |
|---------|------------|------------------|
| MassTransit (default) | `rabbitmq_comprehensive` (Gateway only) | MassTransit `IBus` resolves (RabbitMQ connectivity at startup) |
| Wolverine | `wolverine_bus` (Gateway + Admin) | Postgres message store reachable; reports inbox/outbox/scheduled/dead-letter counts in the health entry data |

`wolverine_bus` surfaces on `/health` and `/health/ready` (tags `messaging`,
`wolverine`, `ready`) and reports:

- **Unhealthy** — the message store is unreachable (the bus cannot persist or
  deliver messages).
- **Degraded** — dead-lettered messages at or above
  `ConduitLLM:Messaging:Wolverine:HealthCheck:DeadLetterDegradedThreshold`
  (default `1`) — messages are exhausting their retries; for the spend/webhook
  queues this warrants investigation.
- It is only registered on the Postgresql transport
  (`ConduitLLM:Messaging:Wolverine:Transport` = `Postgresql`); the in-memory
  dev/CI mode has no message store to probe.

### Bus metrics (`/metrics`, Prometheus)

Both hosts export bus metrics through OpenTelemetry:

- **Wolverine** (meter `Wolverine:{service}`): `wolverine-messages-sent`,
  `wolverine-messages-succeeded`, `wolverine-execution-failure` (tagged by
  exception type), `wolverine-dead-letter-queue`, execution/effective-time
  histograms, and queue-depth gauges `wolverine-inbox-count`,
  `wolverine-outbox-count`, `wolverine-scheduled-count` (Postgres transport).
- **MassTransit** (meter `MassTransit`): built-in consume/publish counters and
  durations — exported so the #929 parity gate can compare backends.

Wolverine message-processing traces are exported under the `Wolverine`
activity source when `Telemetry:TracingEnabled` is on.

Suggested alerts: `wolverine-dead-letter-queue` rate > 0 (financial queues),
`wolverine-inbox-count` sustained growth (consumer lag), health endpoint
Degraded/Unhealthy transitions.