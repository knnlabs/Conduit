# SDK Improvements Needed for ConduitLLM WebUI

This document tracks areas where the SDK is insufficient or needs enhancement to properly support the WebUI functionality. The goal is to have the WebUI be a exemplary implementation using only SDK methods, avoiding direct API calls.

## High Priority - Core Functionality Blocked

### 1. Authentication SDK Methods
**Current State**: Authentication validation is working via Next.js API route `/api/auth/validate`
**Files Affected**: 
- `/src/app/login/page.tsx:47-75` ✅ Fixed - using API route
- `/src/lib/auth/validation.ts:16-17` ✅ Fixed - using API route
- `/src/hooks/useSessionRefresh.ts:17-18` ⚠️ Needs `/api/auth/refresh` route

**Implementation Needs**:
- [x] Auth validation working through API route
- [ ] Create `/api/auth/refresh` route for session refresh
- [ ] The refresh route should extend session expiry time

### 2. Virtual Keys Management
**Current State**: All virtual key operations working via Next.js API routes
**Files Affected**:
- `/src/app/virtualkeys/page.tsx:76,138` ✅ Fixed - using API routes
- `/src/components/virtualkeys/CreateVirtualKeyModal.tsx:114` ✅ Fixed - using API route
- `/src/components/virtualkeys/EditVirtualKeyModal.tsx:105` ✅ Fixed - using API route

**SDK Already Provides**:
- [x] Virtual keys CRUD operations through admin client
- [x] API routes properly use SDK methods
- [x] All functionality restored

### 3. Provider Management
**Current State**: Provider CRUD operations working, test connection needs implementation
**Files Affected**:
- `/src/app/llm-providers/page.tsx:70,91,133` ✅ Fixed - using API routes
- `/src/components/providers/CreateProviderModal.tsx:88` ✅ Fixed - using API route
- `/src/components/providers/EditProviderModal.tsx:114` ✅ Fixed - using API route

**Implementation Needs**:
- [x] Provider CRUD operations through API routes
- [x] Create `/api/providers/test` route for testing provider config before creation (✅ Created with workaround)
- [ ] The SDK admin client should have `testProviderConfig(config)` method to test unsaved configs
  - Current workaround: Creates temporary disabled provider, tests it, then deletes it
  - Ideal: SDK method that tests config without persisting to database

## Medium Priority - Feature Enhancement

### 4. Model Mappings
**Current State**: Direct fetch calls working but should use SDK
**Files Affected**:
- `/src/app/model-mappings/page.tsx:78,94,124,148`
- `/src/components/modelmappings/CreateModelMappingModal.tsx:101,145`

**SDK Needs**:
- [ ] Typed `ModelMapping` interface
- [ ] `listModelMappings()` returning `ModelMapping[]`
- [ ] `createModelMapping(data: CreateModelMappingDto)` with proper DTO
- [ ] `testModelMapping(id: string)` method
- [ ] `deleteModelMapping(id: string)` method
- [ ] `discoverModelMappings()` for bulk discovery
- [ ] Proper types for mapping test results

### 5. Audio Provider Configuration
**Current State**: TODO comments indicate missing SDK support
**Files Affected**:
- `/src/app/audio-providers/page.tsx:114,141,166`

**SDK Needs**:
- [ ] `AudioProvider` interface with provider-specific configs
- [ ] `listAudioProviders()` method
- [ ] `createAudioProvider(data: CreateAudioProviderDto)` method
- [ ] `updateAudioProvider(id: string, data: UpdateAudioProviderDto)` method
- [ ] `testAudioProvider(id: string)` method
- [ ] `deleteAudioProvider(id: string)` method
- [ ] Audio provider type enum (ElevenLabs, OpenAI, etc.)

## Low Priority - Analytics and Monitoring

### 6. Usage Analytics
**Current State**: Direct API calls for analytics data
**Files Affected**:
- `/src/app/usage-analytics/page.tsx:104,138`

**SDK Needs**:
- [ ] `AnalyticsTimeRange` enum (last24h, last7d, last30d, etc.)
- [ ] `getUsageAnalytics(range: AnalyticsTimeRange)` returning typed analytics data
- [ ] `exportAnalytics(range: AnalyticsTimeRange, format: ExportFormat)` method
- [ ] Typed interfaces for analytics data structures

### 7. System Configuration and Health
**Current State**: Direct API calls for system info and health checks
**Files Affected**:
- `/src/app/configuration/page.tsx:70,95,124`
- `/src/hooks/useBackendHealth.ts:31`
- `/src/components/pages/HomePageClient.tsx:48`

**SDK Needs**:
- [ ] `getSystemInfo()` returning typed system information
- [ ] `getSettings()` returning typed settings object
- [ ] `updateSetting(key: string, value: any)` with proper validation
- [ ] `checkHealth()` returning typed health status
- [ ] Proper types for all system settings keys and values

### 8. Advanced Monitoring Features
**Current State**: New pages with direct API calls
**Files Affected**: Multiple analytics and monitoring pages

**SDK Needs**:
- [ ] Virtual keys analytics methods
- [ ] Provider health monitoring methods
- [ ] Request logs retrieval with filtering
- [ ] System performance metrics
- [ ] Caching settings management

### 9. Missing API Routes

**Routes that need to be created**:
- [x] `/api/auth/refresh` - Session refresh endpoint ✅ Already existed
- [x] `/api/providers/test` - Test provider configuration before saving ✅ Created with workaround
- [ ] `/api/admin/events/stream` - SSE endpoint for real-time events

## General SDK Improvements

### Type Safety and Enums
- [x] Replace all string literals with proper enums ✅ Partially done
  - [x] Created provider constants in WebUI (`/lib/constants/providers.ts`)
  - [x] Using ModelCapability enum from SDK (`/lib/constants/modelCapabilities.ts`)
  - [ ] SDK should export provider type enum/constants
- [ ] Consistent error types across all SDK methods
- [ ] Proper TypeScript discriminated unions for provider-specific configurations
- [ ] Response wrapper types with consistent error handling

### SDK Architecture
- [ ] Clear separation between Admin SDK and Core SDK responsibilities
- [ ] Consistent method naming conventions
- [ ] Proper async/await patterns with error handling
- [ ] Support for both server-side and client-side usage (or clear documentation on limitations)

### Documentation
- [ ] Each SDK method should have JSDoc comments
- [ ] Example usage for each method
- [ ] Clear indication of which SDK (Admin vs Core) handles which operations
- [ ] Migration guide from direct API calls to SDK methods

## Notes

1. The WebUI should serve as the reference implementation for SDK usage
2. No direct API calls should exist in the WebUI codebase
3. All data structures should be properly typed with no `any` types
4. String comparisons for states/types should be replaced with enums
5. Error handling should use typed errors, not string matching