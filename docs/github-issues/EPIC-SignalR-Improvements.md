# EPIC: SignalR Reliability and Performance Improvements

## Overview
Enhance the SignalR implementation in Conduit to improve message delivery reliability, performance, monitoring, and error handling. This epic addresses current limitations in real-time message processing and adds enterprise-grade features for production deployments.

## Business Value
- **Reliability**: Ensure critical messages (task updates, notifications) are delivered even during network issues
- **Performance**: Reduce network overhead and improve scalability for high-volume scenarios
- **Observability**: Gain insights into connection health and message flow for troubleshooting
- **User Experience**: Provide seamless real-time updates with automatic recovery from failures

## Success Criteria
- [ ] Zero message loss for critical updates during transient network failures
- [ ] 50% reduction in network overhead through message batching
- [ ] Real-time monitoring dashboard showing connection health metrics
- [ ] Automatic recovery from connection failures within 30 seconds
- [ ] Support for 1000+ concurrent connections per server

## Technical Approach
Implement a layered approach starting with core reliability features, then adding performance optimizations and monitoring capabilities.

### Current Status (as of 2025-08-08)
- Implemented: message acknowledgment (hub + service), reliable queue with retries and circuit breaker, connection monitoring, batching, metrics (Prometheus + filter), SignalR rate limiting filter, Redis backplane.
- Partially implemented: metrics breadth (delivered vs failed per hub/method varies by component), rate limiting breadth (primarily per-virtual-key filter), operational dashboards.
- Not implemented: MessagePack/binary protocol for payload compression, delta updates, dedicated SignalR health checks, diagnostic hub filter, client reconnection/ack patterns, integration and load testing suites.
- References: `ConduitLLM.Http/SignalR/Hubs/AcknowledgmentHub.cs`, `ConduitLLM.Http/SignalR/Services/SignalRAcknowledgmentService.cs`, `ConduitLLM.Http/SignalR/Services/SignalRMessageQueueService.cs`, `ConduitLLM.Http/SignalR/Services/SignalRConnectionMonitor.cs`, `ConduitLLM.Http/SignalR/Services/SignalRMessageBatcher.cs`, `ConduitLLM.Http/Filters/SignalRMetricsFilter.cs`, `ConduitLLM.Http/Authentication/VirtualKeySignalRRateLimitFilter.cs`, `ConduitLLM.Http/Program.SignalR.cs`.

---

## Phase 1: Core Reliability (Priority: High)

### Task 1.1: Implement Message Acknowledgment Pattern
**Story Points**: 5
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `reliability`

**Description**:
Implement a message acknowledgment pattern to ensure critical messages are delivered and processed by clients.

**Acceptance Criteria**:
- [x] Create `SignalRMessage` base class with MessageId, Timestamp, CorrelationId
- [x] Add acknowledgment methods to hubs (`AcknowledgeMessage`, `NackMessage`)
- [x] Implement timeout handling for unacknowledged messages
- [x] Add retry logic for failed messages
- [ ] Unit tests for acknowledgment flow

**Technical Details**:
```csharp
public abstract class SignalRMessage
{
    public string MessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? CorrelationId { get; set; }
    public int RetryCount { get; set; }
}
```

---

### Task 1.2: Create SignalR Message Queue Service
**Story Points**: 8
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `reliability`

**Description**:
Build a background service that queues SignalR messages and handles delivery with retry logic and circuit breaker pattern.

**Acceptance Criteria**:
- [x] Implement `SignalRMessageQueueService` as IHostedService
- [x] Add Polly retry policy with exponential backoff
- [x] Implement circuit breaker for failing endpoints
- [x] Add dead letter queue for persistent failures
- [x] Configure max retry attempts and timeout values
- [ ] Integration tests for queue processing

**Dependencies**: Task 1.1

---

### Task 1.3: Add Connection State Monitoring
**Story Points**: 5
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `monitoring`

**Description**:
Implement comprehensive connection monitoring to track active connections, subscriptions, and connection health.

**Acceptance Criteria**:
- [x] Create `SignalRConnectionMonitor` service
- [x] Track connections by hub, virtual key, and groups
- [x] Monitor connection duration and last activity
- [x] Implement stale connection cleanup (configurable timeout)
- [ ] Expose metrics via health check endpoint
- [x] Add connection metrics to SecureHub base class

**Technical Details**:
- Store connection info in ConcurrentDictionary
- Background timer for cleanup every 5 minutes
- Health check warns if >10% connections are stale

---

## Phase 2: Performance Optimization (Priority: Medium)

### Task 2.1: Implement Message Batching
**Story Points**: 5
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `performance`

**Description**:
Batch multiple messages within a time window to reduce network overhead and improve client performance.

**Acceptance Criteria**:
- [x] Create `SignalRMessageBatcher` service
- [x] Configure batch window (default 100ms) and max batch size (default 50)
- [x] Group messages by type for efficient delivery
- [x] Support immediate send when batch is full
- [x] Add batch metrics (messages per batch, batch frequency)
- [ ] Client-side support for batch message processing

**Dependencies**: Task 1.1

---

### Task 2.2: Add Message Compression
**Story Points**: 3
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `performance`

**Description**:
Enable message compression using MessagePack protocol for binary serialization.

**Acceptance Criteria**:
- [ ] Add MessagePack protocol to SignalR configuration
- [ ] Configure compression settings
- [ ] Update clients to support MessagePack
- [ ] Benchmark performance improvement
- [ ] Document client library requirements

Status: Not implemented as of 2025-08-08.

---

### Task 2.3: Implement Delta Updates
**Story Points**: 5
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `performance`

**Description**:
Send only changed properties in update messages to reduce payload size.

**Acceptance Criteria**:
- [ ] Create delta tracking for task progress updates
- [ ] Implement `TaskProgressDelta` message type
- [ ] Update notification services to send deltas
- [ ] Client-side delta merging logic
- [ ] Unit tests for delta calculation

---

## Phase 3: Enhanced Monitoring (Priority: Medium)

### Task 3.1: Create SignalR Metrics Service
**Story Points**: 5
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `monitoring`, `metrics`

**Description**:
Implement comprehensive metrics collection for SignalR operations.

**Acceptance Criteria**:
- [x] Create metrics with Prometheus/OpenTelemetry counters and histograms
- [ ] Track messages delivered/failed by hub and method
- [x] Measure message delivery duration
- [x] Monitor active connections and groups
- [x] Export metrics to Prometheus/Grafana
- [ ] Create Grafana dashboard template

Note: Implemented via `ISignalRMetrics`/`SignalRMetrics` and `SignalRMetricsService` with Prometheus exporters and hub filters.

**Metrics to Track**:
- `signalr_messages_delivered_total`
- `signalr_messages_failed_total`
- `signalr_message_delivery_duration_seconds`
- `signalr_active_connections`
- `signalr_active_groups`

---

### Task 3.2: Add SignalR Health Checks
**Story Points**: 3
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `monitoring`

**Description**:
Implement health checks for SignalR infrastructure.

**Acceptance Criteria**:
- [ ] Create `SignalRHealthCheck` implementing IHealthCheck
- [ ] Check connection/disconnection ratio
- [ ] Monitor message queue depth
- [ ] Verify hub accessibility
- [ ] Add to health check endpoint
- [ ] Configure warning/critical thresholds

---

### Task 3.3: Implement Diagnostic Logging
**Story Points**: 3
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `monitoring`

**Description**:
Add detailed diagnostic logging for troubleshooting SignalR issues.

**Acceptance Criteria**:
- [ ] Create `SignalRDiagnosticFilter` implementing IHubFilter
- [ ] Log method invocations with duration
- [ ] Add correlation IDs to all log entries
- [ ] Implement configurable log levels
- [ ] Add performance warnings for slow operations

---

## Phase 4: Client-Side Enhancements (Priority: Low)

### Task 4.1: Improve Client Reconnection Logic
**Story Points**: 3
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `client`

**Description**:
Enhance client-side reconnection with exponential backoff and connection quality monitoring.

**Acceptance Criteria**:
- [ ] Implement exponential backoff reconnection strategy
- [ ] Add connection quality monitoring (ping/latency)
- [ ] Show connection status in UI
- [ ] Handle reconnection gracefully without data loss
- [ ] Update both WebUI and SDK clients

---

### Task 4.2: Add Client-Side Message Acknowledgment
**Story Points**: 3
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `client`

**Description**:
Implement client-side message acknowledgment for reliable message processing.

**Acceptance Criteria**:
- [ ] Add acknowledgment logic to WebUI SignalR service
- [ ] Handle message processing errors
- [ ] Send NACK for failed messages
- [ ] Update SDK clients with acknowledgment support
- [ ] Document acknowledgment pattern for clients

**Dependencies**: Task 1.1

---

## Phase 5: Security and Testing (Priority: Low)

### Task 5.1: Add SignalR Rate Limiting
**Story Points**: 3
**Assignee**: TBD
**Labels**: `enhancement`, `signalr`, `security`

**Description**:
Implement rate limiting for SignalR connections and method invocations.

**Acceptance Criteria**:
 - [x] Configure rate limiting policy for SignalR endpoints
- [ ] Limit connections per IP address
- [ ] Limit method invocations per connection
- [ ] Add rate limit headers to responses
- [ ] Document rate limits in API documentation

---

### Task 5.2: Create SignalR Integration Test Suite
**Story Points**: 5
**Assignee**: TBD
**Labels**: `testing`, `signalr`

**Description**:
Build comprehensive integration tests for SignalR functionality.

**Acceptance Criteria**:
- [ ] Test message delivery with acknowledgments
- [ ] Test reconnection scenarios
- [ ] Test batch message processing
- [ ] Test concurrent connections (load test)
- [ ] Test authorization and group access
- [ ] Add to CI/CD pipeline

---

### Task 5.3: Create SignalR Load Testing Framework
**Story Points**: 5
**Assignee**: TBD
**Labels**: `testing`, `signalr`, `performance`

**Description**:
Build load testing framework to validate SignalR scalability.

**Acceptance Criteria**:
- [ ] Create load test scenarios (100, 500, 1000 concurrent connections)
- [ ] Measure message throughput and latency
- [ ] Test connection limits and resource usage
- [ ] Generate performance reports
- [ ] Document performance baselines

---

## Implementation Notes

### Order of Implementation
1. Start with Phase 1 (Core Reliability) - these are critical for production
2. Phase 2 and 3 can be worked on in parallel
3. Phase 4 and 5 are nice-to-have improvements

### Technical Considerations
- All services should be registered as singletons or scoped appropriately
- Use dependency injection for all new services
- Follow existing code patterns and conventions
- Add comprehensive XML documentation
- Include unit tests with minimum 80% coverage

### Breaking Changes
- Task 1.1 will require client updates to support acknowledgments
- Task 2.2 requires client MessagePack support
- Document all breaking changes in release notes

### Configuration
All new features should be configurable via appsettings.json:
```json
{
  "SignalR": {
    "Acknowledgment": {
      "MessageTimeoutSeconds": 30,
      "MaxRetryAttempts": 3
    },
    "Batching": {
      "WindowMs": 100,
      "MaxBatchSize": 50,
      "MaxBatchSizeBytes": 262144,
      "GroupByMethod": true
    },
    "Connection": {
      "StaleConnectionTimeoutMinutes": 60
    },
    "MaximumReceiveMessageSize": 32768
  }
}
```

### Documentation Updates Required
- Update SignalR architecture documentation
- Add troubleshooting guide for common issues
- Create performance tuning guide
- Update client SDK documentation
- Add monitoring setup guide

---

## Follow-up Tickets (created 2025-08-08)

### FT-1: Adopt MessagePack for SignalR payloads
Labels: `signalr`, `performance`
- Description: Enable binary serialization to reduce payload size for WebSocket transport.
- Acceptance Criteria:
  - Server adds `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` and calls `AddMessagePackProtocol`
  - Clients updated to use MessagePack
  - Benchmarks show reduced payload/CPU vs JSON

### FT-2: Client reconnection/backoff and UX status
Labels: `signalr`, `client`
- Description: Implement exponential backoff reconnection and surface connection status in UI/SDKs.
- Acceptance Criteria:
  - Backoff with jitter implemented in clients
  - Connection status indicators and events
  - No data loss across reconnects in tests

### FT-3: Client-side acknowledgment support
Labels: `signalr`, `client`, `reliability`
- Description: Implement ack/nack handling in Web UI and SDKs for critical messages.
- Acceptance Criteria:
  - Clients send ack/nack to server APIs
  - Error paths send NACK with reason
  - Docs with examples per client

### FT-4: SignalR integration test suite
Labels: `signalr`, `testing`
- Description: E2E tests covering ack lifecycle, retries, DLQ, batching, and Redis backplane.
- Acceptance Criteria:
  - Tests run in CI
  - Covers ack success/timeout/retry paths
  - Verifies batch window behavior and DLQ

### FT-5: Load testing at scale (Redis backplane)
Labels: `signalr`, `performance`, `testing`
- Description: Load tests for 100/500/1000+ concurrent connections and throughput.
- Acceptance Criteria:
  - Reports latency, throughput, backplane health
  - Pass thresholds agreed with SRE

### FT-6: SignalR health checks
Labels: `signalr`, `monitoring`
- Description: Add `IHealthCheck` for queue depth, connection ratio, hub accessibility.
- Acceptance Criteria:
  - Health check endpoint returns degraded/unhealthy status with thresholds
  - Alerts integrated with existing monitoring

### FT-7: Diagnostic hub filter with structured logging
Labels: `signalr`, `monitoring`
- Description: Add `IHubFilter` to log invocations with correlation IDs and timing.
- Acceptance Criteria:
  - Correlated logs for hub methods
  - Configurable sampling/levels

### FT-8: Rate limit policy hardening
Labels: `signalr`, `security`
- Description: Extend current virtual-key rate limit to include per-IP and per-connection method limits; optional response headers where applicable.
- Acceptance Criteria:
  - Enforce IP-based and per-connection method limits
  - Metrics and alerts for rate limit breaches
  - Documented policies

## Definition of Done
- [ ] Code implemented and reviewed
- [ ] Unit tests written and passing (>80% coverage)
- [ ] Integration tests completed
- [ ] Documentation updated
- [ ] Performance impact measured
- [ ] Breaking changes documented
- [ ] Deployed to staging environment
- [ ] Load tested with expected traffic