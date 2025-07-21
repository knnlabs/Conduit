# Epic: Migrate WebUI to Node.js Client SDKs

## 🎯 Epic Overview

Migrate the Conduit WebUI from direct API calls to using the official Node.js client libraries (`@knn_labs/conduit-admin-client` and `@knn_labs/conduit-core-client`). This migration will demonstrate best practices for using the SDKs in a production administrative interface and improve maintainability.

## 📋 Objectives

1. **Replace all direct API calls** with SDK methods
2. **Integrate SignalR** using the client libraries' built-in support
3. **Simplify authentication** flows using SDK capabilities
4. **Improve error handling** with SDK's built-in mechanisms
5. **Enable real-time features** (webhooks, polling, SignalR)

## 🚀 Benefits

- **Type Safety**: Full TypeScript support with auto-completion
- **Reduced Boilerplate**: No manual fetch calls or header management
- **Built-in Error Handling**: Automatic retry logic and error recovery
- **Real-time Support**: Native SignalR, webhook, and polling integration
- **Maintenance**: Easier updates when SDK features are added

## 📊 Success Criteria

- [ ] All `/api/admin/*` routes use `@knn_labs/conduit-admin-client`
- [ ] All `/api/core/*` routes use `@knn_labs/conduit-core-client`
- [ ] SignalR connections managed through SDK
- [ ] Zero direct `fetch()` calls to Conduit APIs
- [ ] Improved error handling and user feedback
- [ ] Documentation and examples for common patterns

---

## 📝 Sub-Issues to Create

### Issue #1: Setup SDK Client Infrastructure

**Title**: Setup SDK client initialization and configuration

**Description**:
Create the foundational infrastructure for using the Conduit Node.js SDKs throughout the WebUI application.

**Tasks**:
- [ ] Update `server.ts` to properly initialize both Admin and Core clients
- [ ] Create client factory functions with proper error handling
- [ ] Setup environment variable validation for SDK configuration
- [ ] Add client instance caching and connection pooling
- [ ] Create TypeScript interfaces for client options
- [ ] Add logging and monitoring for SDK operations

**Acceptance Criteria**:
- Server-side clients are properly initialized with error handling
- Client instances are reused efficiently
- Environment variables are validated on startup
- Proper TypeScript types are in place

---

### Issue #2: Create SDK Error Handling Utilities

**Title**: Create error handling and response transformation utilities

**Description**:
Build comprehensive error handling utilities that work with the SDK's error types and provide consistent error responses to the frontend.

**Tasks**:
- [ ] Create error type mappings from SDK errors to HTTP responses
- [ ] Build response transformation utilities for SDK responses
- [ ] Add error logging with proper context
- [ ] Create user-friendly error messages
- [ ] Implement retry logic for transient failures
- [ ] Add error boundary components for SDK operations

**Acceptance Criteria**:
- All SDK errors are properly caught and transformed
- Error messages are user-friendly and actionable
- Proper HTTP status codes are returned
- Error context is logged for debugging

---

### Issue #3: Update Authentication Middleware

**Title**: Update authentication middleware for SDK usage

**Description**:
Modify the authentication middleware to work seamlessly with the SDK's built-in authentication mechanisms.

**Tasks**:
- [ ] Update session validation to work with SDK auth
- [ ] Implement master key handling for Admin SDK
- [ ] Add virtual key extraction for Core SDK
- [ ] Create authentication helpers for SDK operations
- [ ] Update session token generation for SignalR
- [ ] Add authentication error handling

**Acceptance Criteria**:
- Authentication works seamlessly with both SDKs
- Session tokens are properly managed
- SignalR authentication is integrated
- Error cases are handled gracefully

---

### Issue #4: Migrate Virtual Keys Routes

**Title**: Migrate Virtual Keys management routes to Admin SDK

**Description**:
Replace all direct API calls in virtual keys routes with Admin SDK methods.

**Files to Update**:
- `/api/admin/virtual-keys/route.ts`
- `/api/admin/virtual-keys/[id]/route.ts`

**Tasks**:
- [ ] Replace GET /virtual-keys with `client.virtualKeys.list()`
- [ ] Replace POST /virtual-keys with `client.virtualKeys.create()`
- [ ] Replace PUT /virtual-keys/:id with `client.virtualKeys.update()`
- [ ] Replace DELETE /virtual-keys/:id with `client.virtualKeys.delete()`
- [ ] Update spend tracking endpoints
- [ ] Add proper error handling

**Example**:
```typescript
// Before
const response = await fetch(`${adminApiUrl}/v1/virtual-keys`, {
  headers: { 'Authorization': `Bearer ${masterKey}` }
});

// After
const client = getServerAdminClient();
const virtualKeys = await client.virtualKeys.list();
```

---

### Issue #5: Migrate Provider Management Routes

**Title**: Migrate Provider management routes to Admin SDK

**Description**:
Replace provider-related API calls with Admin SDK methods.

**Files to Update**:
- `/api/admin/providers/route.ts`
- `/api/admin/providers/[providerId]/route.ts`
- `/api/admin/providers/test-connection/route.ts`

**Tasks**:
- [ ] Replace provider CRUD operations with SDK methods
- [ ] Implement test connection using `client.providers.testConnection()`
- [ ] Update provider health checks
- [ ] Add provider discovery integration
- [ ] Handle provider-specific configurations

---

### Issue #6: Migrate Model Mappings Routes

**Title**: Migrate Model Mappings routes to Admin SDK

**Description**:
Update model mapping endpoints to use the Admin SDK.

**Files to Update**:
- `/api/admin/model-mappings/route.ts`
- `/api/admin/model-mappings/[mappingId]/route.ts`
- `/api/admin/model-mappings/discover/route.ts`

**Tasks**:
- [ ] Replace mapping CRUD with SDK methods
- [ ] Implement discovery using `client.modelMappings.discover()`
- [ ] Add bulk operations support
- [ ] Update validation logic
- [ ] Handle mapping priorities

---

### Issue #7: Migrate Settings Routes

**Title**: Migrate Settings and Configuration routes to Admin SDK

**Description**:
Replace settings management with Admin SDK methods.

**Files to Update**:
- `/api/admin/system/settings/route.ts`
- `/api/admin/analytics/export/route.ts`

**Tasks**:
- [ ] Replace settings GET/PUT with SDK methods
- [ ] Implement category-based settings
- [ ] Add audio configuration management
- [ ] Update export functionality
- [ ] Handle custom settings

---

### Issue #8: Migrate Analytics Routes

**Title**: Migrate Analytics and Reporting routes to Admin SDK

**Description**:
Update analytics endpoints to use the Admin SDK's analytics service.

**Files to Update**:
- `/api/admin/request-logs/route.ts`
- `/api/admin/system/metrics/route.ts`

**Tasks**:
- [ ] Replace request logs with `client.analytics.getRequestLogs()`
- [ ] Implement cost analytics using SDK
- [ ] Add usage metrics integration
- [ ] Update filtering and pagination
- [ ] Add export capabilities

---

### Issue #9: Migrate Security Routes

**Title**: Migrate Security (IP Rules) routes to Admin SDK

**Description**:
Replace IP filtering endpoints with Admin SDK methods.

**Files to Update**:
- `/api/admin/security/ip-rules/route.ts`
- `/api/admin/security/events/route.ts`
- `/api/admin/security/threats/route.ts`

**Tasks**:
- [ ] Replace IP rules CRUD with SDK methods
- [ ] Implement security event tracking
- [ ] Add threat detection integration
- [ ] Update validation logic
- [ ] Handle CIDR ranges

---

### Issue #10: Migrate Chat Routes

**Title**: Migrate Chat completion routes to Core SDK

**Description**:
Replace chat API calls with Core SDK methods.

**Files to Update**:
- `/api/core/chat/completions/route.ts`

**Tasks**:
- [ ] Replace chat completions with `client.chat.create()`
- [ ] Implement streaming using SDK's streaming support
- [ ] Add function calling support
- [ ] Update error handling for chat-specific errors
- [ ] Add usage tracking

**Example**:
```typescript
// Stream chat responses
const stream = await coreClient.chat.createStream({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'Hello' }]
});

for await (const chunk of stream) {
  // Handle chunk
}
```

---

### Issue #11: Migrate Image Generation Routes

**Title**: Migrate Image generation routes to Core SDK

**Description**:
Update image generation to use Core SDK with async support.

**Files to Update**:
- `/api/core/images/generations/route.ts`

**Tasks**:
- [ ] Replace image generation with `client.images.generate()`
- [ ] Implement async generation with webhooks
- [ ] Add task tracking support
- [ ] Update response handling for media URLs
- [ ] Add progress tracking

---

### Issue #12: Migrate Video Generation Routes

**Title**: Migrate Video generation routes to Core SDK

**Description**:
Update video generation endpoints with full async support.

**Files to Update**:
- `/api/core/videos/generations/route.ts`

**Tasks**:
- [ ] Replace video generation with `client.videos.generate()`
- [ ] Implement webhook callbacks
- [ ] Add SignalR progress tracking
- [ ] Handle long-running operations
- [ ] Add cancellation support

---

### Issue #13: Migrate Audio Routes

**Title**: Migrate Audio transcription routes to Core SDK

**Description**:
Update audio endpoints to use Core SDK methods.

**Files to Update**:
- `/api/core/audio/transcriptions/route.ts`

**Tasks**:
- [ ] Replace transcription with `client.audio.transcribe()`
- [ ] Add file upload handling
- [ ] Implement language detection
- [ ] Update response formatting
- [ ] Add support for various audio formats

---

### Issue #14: Migrate Health Routes

**Title**: Migrate Health check routes to Core SDK

**Description**:
Update health endpoints using SDK methods.

**Files to Update**:
- `/api/core/health/route.ts`
- `/api/admin/system/health/route.ts`

**Tasks**:
- [ ] Replace health checks with SDK methods
- [ ] Add comprehensive health metrics
- [ ] Implement dependency checks
- [ ] Add performance metrics
- [ ] Create health dashboard data

---

### Issue #15: Integrate SignalR from SDK

**Title**: Integrate SignalR using SDK's built-in support

**Description**:
Replace custom SignalR implementation with the SDK's SignalR services.

**Tasks**:
- [ ] Replace SignalRManager with SDK's SignalR service
- [ ] Migrate task progress hub to SDK
- [ ] Integrate video/image generation hubs
- [ ] Add spend notification support
- [ ] Implement automatic reconnection
- [ ] Add connection state management

**Example**:
```typescript
// Initialize with SignalR
const coreClient = new ConduitCoreClient({
  baseURL: process.env.CONDUIT_CORE_API_URL,
  apiKey: virtualKey,
  signalR: {
    enabled: true,
    autoConnect: true
  }
});

// Subscribe to events
coreClient.signalR.tasks.onProgress((progress) => {
  console.log('Task progress:', progress);
});
```

---

### Issue #16: Implement Webhook Handling

**Title**: Implement webhook handling for async operations

**Description**:
Add comprehensive webhook support for async operations using SDK capabilities.

**Tasks**:
- [ ] Create webhook endpoint for video generation
- [ ] Create webhook endpoint for image generation
- [ ] Add webhook signature verification
- [ ] Implement retry logic for failed webhooks
- [ ] Add webhook event logging
- [ ] Create webhook testing utilities

---

### Issue #17: Add Polling Fallback

**Title**: Add polling fallback mechanisms using SDK

**Description**:
Implement polling as a fallback when SignalR/webhooks aren't available.

**Tasks**:
- [ ] Implement task polling using SDK options
- [ ] Add exponential backoff support
- [ ] Create polling configuration
- [ ] Add timeout handling
- [ ] Implement progress updates via polling
- [ ] Add polling metrics

---

### Issue #18: Create Migration Guide

**Title**: Create migration guide and best practices

**Description**:
Document the migration process and best practices for using the SDKs.

**Tasks**:
- [ ] Create migration guide for common patterns
- [ ] Document authentication flows
- [ ] Add error handling examples
- [ ] Create troubleshooting guide
- [ ] Add performance best practices
- [ ] Document configuration options

---

### Issue #19: Add Examples

**Title**: Add comprehensive examples for SDK usage

**Description**:
Create example implementations for common use cases.

**Tasks**:
- [ ] Create virtual key management examples
- [ ] Add streaming chat examples
- [ ] Create async generation examples
- [ ] Add SignalR integration examples
- [ ] Create error handling examples
- [ ] Add authentication examples

---

### Issue #20: Update Tests

**Title**: Update tests for SDK usage

**Description**:
Update all tests to work with the SDK implementation.

**Tasks**:
- [ ] Update unit tests for SDK mocking
- [ ] Add integration tests with SDK
- [ ] Create E2E tests for critical flows
- [ ] Add performance tests
- [ ] Update test utilities
- [ ] Add SDK-specific test helpers

---

## 🎯 Definition of Done

- [ ] All direct API calls replaced with SDK methods
- [ ] SignalR fully integrated through SDK
- [ ] Comprehensive error handling implemented
- [ ] All tests passing
- [ ] Documentation complete
- [ ] Performance metrics collected
- [ ] Zero regressions identified