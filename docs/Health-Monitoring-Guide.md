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

1. Navigate to `/health-monitoring` in the WebUI
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
./Scripts/test-health-monitoring.sh

# Run 60-second demo with multiple scenarios
./Scripts/test-health-monitoring.sh demo
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