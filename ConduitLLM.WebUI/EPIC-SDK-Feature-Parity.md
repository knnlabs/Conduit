# Epic: Conduit SDK Feature Parity

## Overview
This epic tracks the implementation of missing features in the Conduit TypeScript/JavaScript SDKs (`@knn_labs/conduit-core-client` and `@knn_labs/conduit-admin-client`) to achieve feature parity with the direct API implementation.

## Background
During the migration of the WebUI from direct API calls to SDK usage, several gaps were identified where the SDK lacks support for features available in the REST API. This epic aims to close those gaps.

## Scope
Add missing features to both Core and Admin SDK clients to support all functionality required by the WebUI and other SDK consumers.

## User Stories

### 1. Analytics API Support
**As a** developer using the Admin SDK  
**I want** to export analytics data programmatically  
**So that** I can integrate analytics into external systems

**Acceptance Criteria:**
- [ ] Implement `adminClient.analytics.export()` method
- [ ] Support all export formats (CSV, JSON, Excel)
- [ ] Include proper TypeScript types
- [ ] Add unit tests with mocked responses
- [ ] Update SDK documentation

### 2. Enhanced Settings Service
**As a** developer using the Admin SDK  
**I want** advanced settings management capabilities  
**So that** I can manage configuration more efficiently

**Acceptance Criteria:**
- [ ] Implement `adminClient.settings.updateCategory()` method
- [ ] Implement `adminClient.settings.getSettingsByCategory()` method
- [ ] Implement `adminClient.settings.update()` generic update method
- [ ] Implement `adminClient.settings.set()` generic set method
- [ ] Add TypeScript interfaces for all methods
- [ ] Add comprehensive tests
- [ ] Update documentation with examples

### 3. Audio Processing API
**As a** developer using the Core SDK  
**I want** to transcribe audio files  
**So that** I can build voice-enabled applications

**Acceptance Criteria:**
- [ ] Implement `coreClient.audio` namespace
- [ ] Implement `coreClient.audio.transcribe()` method
- [ ] Support multiple audio formats (mp3, wav, m4a, etc.)
- [ ] Handle file uploads and streaming
- [ ] Add progress callbacks
- [ ] Include error handling for unsupported formats
- [ ] Add integration tests

### 4. Video Generation API
**As a** developer using the Core SDK  
**I want** to generate videos programmatically  
**So that** I can create video content dynamically

**Acceptance Criteria:**
- [ ] Implement `coreClient.videos` namespace
- [ ] Implement `coreClient.videos.generate()` method
- [ ] Support async task tracking
- [ ] Include progress monitoring
- [ ] Handle large file responses
- [ ] Add TypeScript types for all video parameters
- [ ] Create example implementations

### 5. Health Check API
**As a** developer using the Core SDK  
**I want** to check service health  
**So that** I can monitor system status

**Acceptance Criteria:**
- [ ] Implement `coreClient.health` namespace
- [ ] Implement `coreClient.health.check()` method
- [ ] Return detailed health status
- [ ] Support timeout configuration
- [ ] Add retry logic for health checks
- [ ] Include subsystem health details

### 6. SignalR Real-time Support
**As a** developer using either SDK  
**I want** real-time updates via SignalR  
**So that** I can build reactive applications

**Acceptance Criteria:**
- [ ] Add `signalR` property to both SDK clients
- [ ] Implement connection management (`connect()`, `disconnect()`, `isConnected()`)
- [ ] Support SignalR configuration during client initialization
- [ ] Handle automatic reconnection
- [ ] Add connection state callbacks
- [ ] Support multiple transport protocols
- [ ] Include TypeScript types for all events
- [ ] Add connection pooling for efficiency

### 7. Notifications API
**As a** developer using either SDK  
**I want** to subscribe to real-time notifications  
**So that** I can react to system events

**Acceptance Criteria:**
- [ ] Implement `coreClient.notifications` namespace with:
  - [ ] `onVideoProgress()` - Video generation progress
  - [ ] `onImageProgress()` - Image generation progress
  - [ ] `onSpendUpdate()` - Spending updates
  - [ ] `onSpendLimitAlert()` - Budget alerts
- [ ] Implement `adminClient.notifications` namespace with:
  - [ ] `onNavigationStateUpdate()` - UI state changes
  - [ ] `onModelDiscovered()` - New model availability
  - [ ] `onProviderHealthChange()` - Provider status updates
  - [ ] `onVirtualKeyEvent()` - Key lifecycle events
  - [ ] `onConfigurationChange()` - Settings updates
- [ ] Support event subscription/unsubscription
- [ ] Include proper cleanup methods
- [ ] Add TypeScript event interfaces

### 8. Enhanced Configuration Options
**As a** developer using either SDK  
**I want** advanced configuration options  
**So that** I can customize SDK behavior

**Acceptance Criteria:**
- [ ] Add `retryDelay` configuration option
- [ ] Add `onError` callback for global error handling
- [ ] Add `onRequest` callback for request interception
- [ ] Add `onResponse` callback for response interception
- [ ] Support middleware-style plugins
- [ ] Include request/response transformation
- [ ] Add logging configuration

### 9. Enhanced Chat Completions
**As a** developer using the Core SDK  
**I want** full OpenAI-compatible chat features  
**So that** I can use advanced LLM capabilities

**Acceptance Criteria:**
- [ ] Add `functions` property for function calling
- [ ] Add `function_call` property for function control
- [ ] Add `user` property for user identification
- [ ] Support tool calling format
- [ ] Include proper TypeScript types
- [ ] Add validation for function schemas

### 10. Model Discovery Enhancement
**As a** developer using the Admin SDK  
**I want** to discover available models  
**So that** I can dynamically configure model mappings

**Acceptance Criteria:**
- [ ] Implement `adminClient.modelMappings.discoverModels()` method
- [ ] Support provider-specific discovery
- [ ] Return model capabilities
- [ ] Include model metadata
- [ ] Add caching support
- [ ] Handle discovery failures gracefully

### 11. Streaming Response Enhancement
**As a** developer using the Core SDK  
**I want** robust streaming support  
**So that** I can handle real-time responses efficiently

**Acceptance Criteria:**
- [ ] Improve TypeScript types for streaming responses
- [ ] Add proper async iterator support for SSE
- [ ] Include stream error handling
- [ ] Support stream cancellation
- [ ] Add progress callbacks
- [ ] Optimize memory usage for large streams

### 12. API Consistency Fixes
**As a** developer using either SDK  
**I want** consistent API naming and types  
**So that** I can write maintainable code

**Acceptance Criteria:**
- [ ] Fix `VirtualKeyFilters.includeDisabled` → `isEnabled`
- [ ] Fix `sortBy` to accept `SortOptions` object
- [ ] Align all filter interfaces with API
- [ ] Standardize error response formats
- [ ] Update all affected documentation

## Technical Considerations

### Breaking Changes
- Some fixes (like filter property names) may require breaking changes
- Consider providing migration utilities or compatibility layers
- Use semantic versioning appropriately

### Performance
- SignalR connections should use connection pooling
- Implement efficient event listener management
- Consider lazy loading for optional features

### Testing Strategy
- Unit tests for all new methods
- Integration tests with mock servers
- End-to-end tests with real API (in CI)
- Performance benchmarks for streaming

### Documentation
- Update API reference for all new methods
- Add code examples for each feature
- Create migration guide for breaking changes
- Include troubleshooting section

## Dependencies
- SignalR client library
- File upload libraries for audio/video
- Streaming utilities

## Success Metrics
- [ ] 100% feature parity with REST API
- [ ] All tests passing with >90% coverage
- [ ] Documentation complete
- [ ] No performance regressions
- [ ] WebUI successfully using all features

## Timeline Estimate
- Analytics API: 2 days
- Settings Service: 3 days
- Audio/Video APIs: 5 days
- Health Check: 1 day
- SignalR Support: 5 days
- Notifications API: 4 days
- Configuration Options: 2 days
- Chat Enhancements: 2 days
- Model Discovery: 2 days
- Streaming Enhancement: 3 days
- API Consistency: 2 days

**Total: ~6 weeks**

## Release Plan
1. Version 2.1.0: API Consistency Fixes (breaking changes)
2. Version 2.2.0: Configuration Options & Health API
3. Version 2.3.0: Analytics & Enhanced Settings
4. Version 2.4.0: Audio & Video APIs
5. Version 2.5.0: SignalR & Notifications
6. Version 2.6.0: Chat & Model Discovery Enhancements
7. Version 2.7.0: Streaming Enhancements