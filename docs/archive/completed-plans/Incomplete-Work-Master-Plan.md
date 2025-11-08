# Development Priorities and Technical Tasks

This document outlines development priorities, technical improvements, and planned features for Conduit, organized by priority.

## 🔴 Critical Priority

### 1. ProviderCredentialService Implementation
- **Location**: `ConduitLLM.Services/ProviderCredentialService.cs`
- **Issue**: All 6 database methods are completely unimplemented (return null or throw NotImplementedException)
- **Impact**: Blocks credential management functionality
- **Methods to implement**:
  - `AddCredentialAsync`
  - `DeleteCredentialAsync`
  - `GetAllCredentialsAsync`
  - `GetCredentialByIdAsync`
  - `GetCredentialByProviderNameAsync`
  - `UpdateCredentialAsync`

### 2. Test Infrastructure Restoration
- **RealtimeProxyServiceTests.cs**
  - Line 22: Tests need rewriting for new RealtimeProxyService architecture
  - Line 306: Tests disabled pending interface updates
- **RealtimeWebSocketIntegrationTests.cs**
  - Line 29: Tests need refactoring for new audio API architecture
  - Line 478: Tests need updates for new RealtimeSession architecture
- **DatabaseHealthCheckTests.cs**
  - Line 19: Tests need updates to match current IDatabaseConnectionFactory interface

## 🟠 High Priority (Production Readiness)

### 3. Audio API Enhancements

#### Monitoring & Observability
- [ ] Audio-specific metrics and dashboards
- [ ] Real-time alerting for audio issues
- [ ] Distributed tracing integration
- [ ] Performance monitoring

#### Load Testing
- [ ] Concurrent session stress testing
- [ ] Provider failover testing
- [ ] Latency measurements under load
- [ ] Resource utilization analysis

#### Production Readiness
- [ ] Circuit breakers for providers
- [ ] Graceful degradation strategies
- [ ] Disaster recovery procedures
- [ ] Operational documentation

#### Security & Polish
- [ ] Security audit
- [ ] Performance benchmarking
- [ ] Customer beta testing
- [ ] Documentation and tutorials
- [ ] Marketing materials

### 4. Exception Handling Improvements
Critical issues found:
- **Empty catch blocks**: `OpenAIRealtimeSession.cs` (lines 153, 163)
- **Generic catches without logging**: Multiple files swallow exceptions
- **Lost stack traces**: Re-throwing without preserving inner exceptions
- **Inconsistent error handling**: Some catches log, others silently fail

## 🟡 Medium Priority (Feature Completion)

### 5. Usage Tracking Enhancements
- **RealtimeProxyService.cs** (line 360): Implement virtual key spend tracking
- **RealtimeUsageTracker.cs** (line 228): Implement function call tracking

### 6. Dynamic Model Configuration
Remove hardcoded model references from:
- **ModelCapabilityDetector**: Hardcoded vision-capable model patterns
- **AudioCapabilityDetector**: Hardcoded audio model definitions
- **TiktokenCounter**: Hardcoded tokenizer model patterns
- **ProviderDefaultModels**: Hardcoded default model selections

## 🟢 Low Priority (Feature Extensions)

### 7. Provider Feature Expansion
**Embedding Support** (NotImplementedException in multiple providers):
- CohereClient.cs (line 311)
- HuggingFaceClient.cs (line 299)
- DefaultLLMRouter.cs (line 896)

**Image Generation**:
- HuggingFaceClient.cs (line 308)

**Bedrock Provider Support**:
- Meta Llama models (line 239)
- Amazon Titan models (line 253)
- Cohere models via Bedrock (line 267)
- AI21 models (line 281)

### 8. Virtual Key Management
**ApiVirtualKeyService.cs** has 5 unimplemented methods:
- GenerateVirtualKeyAsync
- GetVirtualKeyInfoAsync
- ListVirtualKeysAsync
- UpdateVirtualKeyAsync
- DeleteVirtualKeyAsync

### 9. Future Provider Additions
From roadmap documentation:
- Google Cloud Audio Services
- AWS Audio Services
- Anthropic Vision Support

## 📊 Technical Debt Summary

### Testing Issues
- **40+ disabled/skipped tests** across the codebase
- Main reasons: External dependencies, implementation bugs, mocking difficulties
- Critical test files completely disabled due to architecture changes

### Code Quality Issues
- **9 NotImplementedException** instances
- **Multiple empty catch blocks** creating silent failures
- **Hardcoded model references** preventing full dynamic configuration
- **Incomplete TODO items** throughout the codebase

### Architecture Improvements (Roadmap)
**Short-term (Q3 2025)**:
- Complete removal of hardcoded models
- Complete audio phases 8.2-9
- Add Google Cloud and AWS audio providers
- Implement advanced audio analytics

**Medium-term (Q4 2025)**:
- Multi-region support
- Advanced routing algorithms
- Fine-tuning support
- Batch processing APIs

**Long-term (2026)**:
- Plugin architecture
- Custom model hosting
- Edge deployment options
- Advanced analytics

## 🎯 Recommended Action Plan

### Week 1-2: Fix Critical Blockers
1. Implement ProviderCredentialService database methods or document why they're not needed
2. Restore critical test coverage by updating disabled tests

### Week 3-4: Audio API Enhancement
1. Implement monitoring and observability
2. Perform load testing
3. Ensure production readiness

### Week 5-6: Address Technical Debt
1. Fix all empty catch blocks and improve exception handling
2. Remove hardcoded model references
3. Re-enable and fix failing tests

### Week 7-8: Feature Extensions
1. Add embedding support for providers
2. Complete virtual key management
3. Implement remaining Bedrock providers

## Success Metrics

- All critical tests passing
- Zero NotImplementedException in production code
- All exceptions properly logged
- Dynamic model configuration fully implemented
- Audio API 100% complete with monitoring
- Documentation updated for all new features

This plan prioritizes work that unblocks functionality and ensures production readiness before moving to feature extensions.