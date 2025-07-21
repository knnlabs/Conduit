# Admin API Security Endpoints - Implementation Required

This document lists all the security endpoints that need to be implemented in the Admin API to support the security features in the Admin SDK.

## Current State

The Admin API has robust security infrastructure including:
- `ISecurityService` for authentication, rate limiting, and IP filtering
- `SecurityEventMonitoringService` exists in Core API (ConduitLLM.Http)
- `SecurityMonitoringHub` SignalR hub exists in Core API
- IP filtering endpoints are already implemented

However, most security monitoring and management endpoints are not exposed via REST API.

## Required Endpoints

### 1. Security Events (`/api/security/events`)

```csharp
[ApiController]
[Route("api/security/events")]
[Authorize(Policy = "MasterKeyOnly")]
public class SecurityEventsController : ControllerBase
{
    // GET /api/security/events
    [HttpGet]
    public async Task<ActionResult<List<SecurityEventDto>>> GetEvents(
        [FromQuery] SecurityEventFilters filters)
    
    // GET /api/security/events/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<SecurityEventDto>> GetEventById(string id)
    
    // GET /api/security/events/by-ip/{ipAddress}
    [HttpGet("by-ip/{ipAddress}")]
    public async Task<ActionResult<List<SecurityEventDto>>> GetEventsByIp(
        string ipAddress, int limit = 100)
    
    // GET /api/security/events/by-key/{virtualKey}
    [HttpGet("by-key/{virtualKey}")]
    public async Task<ActionResult<List<SecurityEventDto>>> GetEventsByVirtualKey(
        string virtualKey, int limit = 100)
    
    // POST /api/security/events/export
    [HttpPost("export")]
    public async Task<IActionResult> ExportEvents(
        [FromBody] SecurityExportOptionsDto options)
}
```

### 2. Rate Limiting (`/api/security/rate-limits`)

```csharp
[ApiController]
[Route("api/security/rate-limits")]
[Authorize(Policy = "MasterKeyOnly")]
public class RateLimitingController : ControllerBase
{
    // GET /api/security/rate-limits
    [HttpGet]
    public async Task<ActionResult<List<RateLimitRuleDto>>> GetRules()
    
    // GET /api/security/rate-limits/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<RateLimitRuleDto>> GetRuleById(string id)
    
    // POST /api/security/rate-limits
    [HttpPost]
    public async Task<ActionResult<RateLimitRuleDto>> CreateRule(
        [FromBody] CreateRateLimitRuleDto rule)
    
    // PUT /api/security/rate-limits/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRule(
        string id, [FromBody] UpdateRateLimitRuleDto updates)
    
    // DELETE /api/security/rate-limits/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRule(string id)
    
    // GET /api/security/rate-limits/status/{ipAddress}
    [HttpGet("status/{ipAddress}")]
    public async Task<ActionResult<List<RateLimitStatusDto>>> GetRateLimitStatus(
        string ipAddress)
    
    // POST /api/security/rate-limits/reset/{ipAddress}
    [HttpPost("reset/{ipAddress}")]
    public async Task<IActionResult> ResetRateLimit(string ipAddress)
}
```

### 3. Authentication Tracking (`/api/security/banned-ips`, `/api/security/auth`)

```csharp
[ApiController]
[Route("api/security")]
[Authorize(Policy = "MasterKeyOnly")]
public class AuthenticationTrackingController : ControllerBase
{
    // GET /api/security/banned-ips
    [HttpGet("banned-ips")]
    public async Task<ActionResult<List<BannedIpInfoDto>>> GetBannedIPs()
    
    // POST /api/security/banned-ips
    [HttpPost("banned-ips")]
    public async Task<ActionResult<BannedIpInfoDto>> BanIP(
        [FromBody] BanIpRequestDto request)
    
    // POST /api/security/banned-ips/{ipAddress}/unban
    [HttpPost("banned-ips/{ipAddress}/unban")]
    public async Task<IActionResult> UnbanIP(string ipAddress)
    
    // GET /api/security/auth/failed-attempts/{ipAddress}
    [HttpGet("auth/failed-attempts/{ipAddress}")]
    public async Task<ActionResult<List<FailedAuthAttemptDto>>> GetFailedAttempts(
        string ipAddress)
    
    // POST /api/security/auth/failed-attempts/{ipAddress}/clear
    [HttpPost("auth/failed-attempts/{ipAddress}/clear")]
    public async Task<IActionResult> ClearFailedAttempts(string ipAddress)
    
    // GET /api/security/auth/metrics
    [HttpGet("auth/metrics")]
    public async Task<ActionResult<AuthenticationMetricsDto>> GetAuthMetrics()
}
```

### 4. Threat Detection (`/api/security/threats`)

```csharp
[ApiController]
[Route("api/security/threats")]
[Authorize(Policy = "MasterKeyOnly")]
public class ThreatDetectionController : ControllerBase
{
    // GET /api/security/threats
    [HttpGet]
    public async Task<ActionResult<List<ThreatAlertDto>>> GetThreats(
        [FromQuery] int? days)
    
    // GET /api/security/threats/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ThreatAlertDto>> GetThreatById(string id)
    
    // POST /api/security/threats
    [HttpPost]
    public async Task<ActionResult<ThreatAlertDto>> CreateThreat(
        [FromBody] CreateThreatAlertDto threat)
    
    // POST /api/security/threats/{id}/acknowledge
    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgeThreat(
        string id, [FromBody] AcknowledgeThreatDto request)
    
    // GET /api/security/threats/ip-risk/{ipAddress}
    [HttpGet("ip-risk/{ipAddress}")]
    public async Task<ActionResult<IpRiskScoreDto>> GetIPRiskScore(string ipAddress)
}
```

### 5. Security Dashboard (`/api/security/dashboard`)

```csharp
[ApiController]
[Route("api/security")]
[Authorize(Policy = "MasterKeyOnly")]
public class SecurityDashboardController : ControllerBase
{
    // GET /api/security/dashboard
    [HttpGet("dashboard")]
    public async Task<ActionResult<SecurityDashboardDto>> GetDashboard()
    
    // GET /api/security/metrics
    [HttpGet("metrics")]
    public async Task<ActionResult<SecurityMetricsDto>> GetMetrics()
    
    // GET /api/security/metrics/realtime
    [HttpGet("metrics/realtime")]
    public async Task<ActionResult<SecurityMetricsDto>> GetRealtimeMetrics()
    
    // GET /api/security/dashboard/top-threats
    [HttpGet("dashboard/top-threats")]
    public async Task<ActionResult<List<IpStatsDto>>> GetTopThreats(int limit = 10)
    
    // GET /api/security/dashboard/trends
    [HttpGet("dashboard/trends")]
    public async Task<ActionResult<SecurityTrendsDto>> GetTrends(string period)
    
    // POST /api/security/dashboard/export
    [HttpPost("dashboard/export")]
    public async Task<IActionResult> ExportReport(
        [FromBody] SecurityExportOptionsDto options)
}
```

### 6. Security Monitoring Hub (`/hubs/security-monitoring`)

```csharp
[Authorize(Policy = "MasterKeyOnly")]
public class SecurityMonitoringHub : Hub
{
    // Connection lifecycle
    public override async Task OnConnectedAsync()
    public override Task OnDisconnectedAsync(Exception? exception)
    
    // Hub methods
    public async Task<SecurityMetrics> GetSecurityMetrics()
    public async Task<List<SecurityEvent>> GetRecentSecurityEvents(int minutes = 60)
    
    // Streaming
    public async IAsyncEnumerable<SecurityEvent> StreamSecurityEvents(
        SecurityEventType[] eventTypes)
    
    // Subscriptions
    public async Task SubscribeToThreatLevel()
    public async Task UnsubscribeFromThreatLevel()
    public async Task SubscribeToEventType(SecurityEventType eventType)
    public async Task UnsubscribeFromEventType(SecurityEventType eventType)
    
    // Server-to-client methods:
    // - SecurityEvent
    // - ThreatDetected
    // - SecurityMetricsUpdate
    // - IpBanned
    // - ThreatLevelChange
}
```

## Implementation Steps

1. **Move Security Services to Shared Project**
   - Move `SecurityEventMonitoringService` from ConduitLLM.Http to ConduitLLM.Core
   - Share between Core API and Admin API

2. **Create DTOs**
   - Add security DTOs to ConduitLLM.Configuration
   - Map between domain models and DTOs

3. **Implement Controllers**
   - Add controllers to ConduitLLM.Admin
   - Use existing `ISecurityService` where possible
   - Access security monitoring data from shared service

4. **Add SignalR Hub**
   - Copy `SecurityMonitoringHub` from Core API to Admin API
   - Register in Admin API startup
   - Ensure master key authentication

5. **Database Considerations**
   - Security events may need persistent storage
   - Consider adding SecurityEvent table
   - Or use Redis with longer TTL

## Testing

Each endpoint should have:
- Unit tests for business logic
- Integration tests for API endpoints
- SignalR hub connection tests
- Authorization tests (master key only)

## Security Considerations

1. All endpoints require master key authentication
2. Consider rate limiting on security endpoints themselves
3. Sanitize IP addresses in logs
4. Be careful with sensitive data in export endpoints
5. Implement audit logging for security configuration changes

## Priority

High priority endpoints (needed for basic functionality):
1. Security events listing and filtering
2. Banned IPs management
3. Basic metrics

Medium priority:
1. Rate limit management
2. Threat detection
3. Dashboard aggregations

Low priority:
1. Export functionality
2. SignalR real-time updates
3. Advanced analytics