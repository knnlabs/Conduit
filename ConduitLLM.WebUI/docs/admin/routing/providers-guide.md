# Providers Guide

## Managing Provider Priorities

This guide explains how to configure and manage provider priorities in the Conduit routing system.

## Understanding Provider Priority

Provider priority determines the default order in which providers are selected when no specific routing rules apply or when fallback is needed. This creates a robust fallback chain that ensures high availability and optimal performance.

### Priority Concepts

- **Priority Number**: Lower numbers indicate higher priority (1 = highest priority)
- **Fallback Chain**: Automatic progression from primary to secondary to tertiary providers
- **Load Balancing**: Distribution of requests across providers with similar priorities
- **Health Checking**: Automatic failover when providers become unavailable

## Accessing Provider Priority Management

1. Navigate to **Routing Settings** in the admin dashboard
2. Click on the **Provider Priority** tab
3. View and modify the current provider priority configuration

## Provider Priority Interface

### Provider List

The provider list displays all configured providers with their current settings:

- **Provider Name**: Descriptive name of the provider
- **Type**: Provider category (primary, backup, special)
- **Priority**: Current priority number (1 = highest)
- **Status**: Enabled/Disabled state
- **Health**: Current provider health status
- **Response Time**: Average response time metrics
- **Success Rate**: Percentage of successful requests

### Priority Management Controls

**Drag and Drop Reordering:**
- Drag providers up or down to change their priority order
- Changes are applied automatically when you drop the provider

**Manual Priority Setting:**
- Click the priority number to edit it directly
- Enter a new priority value (1-1000)
- Lower numbers = higher priority

**Bulk Actions:**
- Select multiple providers using checkboxes
- Apply actions to multiple providers at once:
  - Enable/Disable selected providers
  - Set priority range
  - Configure load balancing groups

**Enable/Disable Providers:**
- Toggle the enabled status for each provider
- Disabled providers are skipped in the routing chain
- Use for maintenance or temporary provider issues

## Provider Configuration

### Basic Provider Settings

**Provider Name:**
- Descriptive identifier for the provider
- Should clearly indicate the provider's purpose or location
- Examples: "OpenAI Primary", "Azure EU West", "Local Fallback"

**Provider Type:**
- **Primary**: Main production providers for regular traffic
- **Backup**: Fallback providers for high availability
- **Special**: Specialized providers for specific use cases

**Priority Number:**
- Range: 1-1000 (1 = highest priority)
- Gaps are allowed (e.g., 10, 20, 30) for easier reordering
- Same priority providers are load-balanced

**Enabled Status:**
- Controls whether the provider participates in routing
- Disabled providers are excluded from all routing decisions
- Useful for maintenance or provider issues

### Advanced Provider Settings

**Health Check Configuration:**
- **Endpoint**: URL for health checks
- **Interval**: How often to check provider health
- **Timeout**: Maximum time to wait for health check response
- **Retry Count**: Number of failed checks before marking unhealthy

**Performance Thresholds:**
- **Response Time**: Maximum acceptable response time
- **Success Rate**: Minimum required success rate
- **Capacity**: Maximum concurrent requests

**Load Balancing Settings:**
- **Weight**: Relative weight for load balancing (when priorities are equal)
- **Max Connections**: Maximum concurrent connections
- **Connection Pool**: Connection pooling configuration

## Common Provider Priority Patterns

### 1. Simple Primary/Secondary/Tertiary

**Use Case**: Basic high availability setup

```
Priority 1: OpenAI Primary (Primary)
Priority 2: Azure Secondary (Backup)  
Priority 3: Local Fallback (Backup)
```

**Behavior**: Always try OpenAI first, fall back to Azure if unavailable, use local as last resort.

### 2. Geographic Priority with Regional Fallbacks

**Use Case**: Optimize for regional performance

```
Priority 1: Azure US East (Primary)
Priority 2: Azure US West (Primary)
Priority 10: OpenAI Global (Backup)
Priority 20: Local Fallback (Backup)
```

**Behavior**: Prefer Azure US regions, fall back to OpenAI globally, then local.

### 3. Cost-Optimized with Performance Fallback

**Use Case**: Balance cost and performance

```
Priority 1: Cost-Effective Provider (Primary)
Priority 2: Balanced Provider (Primary)
Priority 5: Premium Provider (Backup)
Priority 10: Local Fallback (Backup)
```

**Behavior**: Try cost-effective first, upgrade to premium if needed.

### 4. Load Balanced Primary Providers

**Use Case**: Distribute load across multiple equivalent providers

```
Priority 1: OpenAI Provider A (Primary)
Priority 1: OpenAI Provider B (Primary)
Priority 1: Azure Provider C (Primary)
Priority 10: Backup Provider (Backup)
```

**Behavior**: Load balance across the three priority 1 providers, fall back to backup.

### 5. Specialized Provider Routing

**Use Case**: Different providers for different capabilities

```
Priority 1: OpenAI GPT-4 (Special - for GPT models)
Priority 1: Anthropic Claude (Special - for Claude models)
Priority 1: Azure OpenAI (Primary - general purpose)
Priority 10: Local Fallback (Backup)
```

**Behavior**: Route to specialized providers based on model, with general fallback.

## Provider Health and Monitoring

### Health Status Indicators

**Healthy (Green):**
- Provider is responding normally
- Response times within acceptable limits
- Success rate above threshold

**Degraded (Yellow):**
- Provider is responding but with reduced performance
- Response times elevated but acceptable
- Success rate slightly below optimal

**Unhealthy (Red):**
- Provider is not responding or failing frequently
- Response times exceed maximum threshold
- Success rate below minimum requirement

**Unknown (Gray):**
- Health check status cannot be determined
- Provider may be newly configured
- Health checking disabled

### Automatic Failover

The system automatically handles provider failures:

1. **Health Monitoring**: Continuous monitoring of provider health
2. **Automatic Failover**: Switch to next priority provider on failure
3. **Recovery Detection**: Automatically restore providers when they recover
4. **Circuit Breaker**: Temporary isolation of failing providers

### Performance Metrics

**Response Time Tracking:**
- Average response time over time
- 95th percentile response times
- Response time trends and patterns

**Success Rate Monitoring:**
- Percentage of successful requests
- Error rate trends
- Common error types and patterns

**Throughput Metrics:**
- Requests per second handling capacity
- Concurrent request limits
- Peak usage patterns

## Load Balancing Strategies

### Round Robin

Distribute requests evenly across providers with the same priority:

```
Request 1 → Provider A
Request 2 → Provider B  
Request 3 → Provider C
Request 4 → Provider A (cycle repeats)
```

### Weighted Round Robin

Distribute requests based on provider weights:

```
Provider A (Weight: 3) → Gets 3/6 of requests
Provider B (Weight: 2) → Gets 2/6 of requests
Provider C (Weight: 1) → Gets 1/6 of requests
```

### Least Connections

Route to the provider with the fewest active connections:

```
Provider A (5 active connections) → Selected
Provider B (8 active connections)
Provider C (12 active connections)
```

### Response Time Based

Route to the provider with the best response time:

```
Provider A (50ms avg) → Selected
Provider B (100ms avg)
Provider C (150ms avg)
```

## Best Practices

### Priority Assignment

1. **Use Meaningful Gaps**: Assign priorities like 10, 20, 30 instead of 1, 2, 3 for easier reordering
2. **Group by Function**: Use priority ranges for different provider types
3. **Reserve Low Numbers**: Keep priorities 1-5 for critical primary providers
4. **Document Reasoning**: Maintain documentation explaining priority decisions

### Provider Management

1. **Regular Health Checks**: Monitor provider health and performance regularly
2. **Capacity Planning**: Ensure providers can handle expected load
3. **Maintenance Windows**: Plan for provider maintenance and updates
4. **Cost Monitoring**: Track usage and costs across providers

### High Availability

1. **Multiple Fallbacks**: Always configure at least 2-3 fallback providers
2. **Geographic Distribution**: Use providers in different regions when possible
3. **Diverse Technologies**: Mix different provider types to avoid single points of failure
4. **Test Failover**: Regularly test failover scenarios

### Performance Optimization

1. **Monitor Metrics**: Track response times and success rates
2. **Adjust Priorities**: Reorder providers based on performance data
3. **Load Balance**: Use equal priorities for equivalent providers
4. **Optimize Routing**: Consider geographic proximity and capabilities

## Troubleshooting

### Provider Not Being Used

**Symptoms**: Expected provider not receiving requests

**Possible Causes**:
- Provider has lower priority than others
- Provider is disabled
- Provider is marked unhealthy
- Routing rules override priority selection

**Solutions**:
1. Check provider priority and adjust if needed
2. Verify provider is enabled
3. Check provider health status
4. Review routing rules for conflicts

### Poor Performance

**Symptoms**: Slow response times or high error rates

**Possible Causes**:
- Provider is overloaded
- Network connectivity issues
- Provider capacity limits
- Incorrect load balancing configuration

**Solutions**:
1. Check provider performance metrics
2. Verify network connectivity
3. Adjust load balancing weights
4. Add additional providers to distribute load

### Failover Not Working

**Symptoms**: Requests fail instead of falling back to secondary providers

**Possible Causes**:
- Health checks not configured properly
- Failover timeout too long
- Secondary providers also unavailable
- Circuit breaker preventing retries

**Solutions**:
1. Configure proper health checks
2. Adjust failover timeouts
3. Verify secondary provider availability
4. Check circuit breaker settings

### Inconsistent Routing

**Symptoms**: Requests going to unexpected providers

**Possible Causes**:
- Routing rules overriding priorities
- Load balancing configuration issues
- Provider health status changes
- Caching of routing decisions

**Solutions**:
1. Review active routing rules
2. Check load balancing settings
3. Monitor provider health status
4. Clear routing caches if applicable

## Advanced Configuration

### Custom Health Checks

Configure custom health check endpoints:

```yaml
health_check:
  endpoint: "/health"
  method: "GET"
  timeout: 5000
  interval: 30000
  success_codes: [200, 204]
  failure_threshold: 3
```

### Provider Groups

Organize providers into logical groups:

```yaml
groups:
  primary:
    - openai-primary
    - azure-primary
  cost_effective:
    - local-model
    - open-source-api
  premium:
    - anthropic-claude
    - openai-gpt4
```

### Dynamic Priority Adjustment

Configure automatic priority adjustment based on performance:

```yaml
dynamic_priority:
  enabled: true
  metric: "response_time"
  adjustment_interval: 300000
  max_priority_change: 5
```

For more detailed examples and advanced configurations, see the [Examples](./examples/) directory.