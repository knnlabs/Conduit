# Direct Fetch() Calls Audit - ConduitLLM WebUI

This document lists all components and hooks that are still using direct fetch() calls to backend services instead of using the SDK pattern.

## Summary

Total files with direct fetch calls: **42 files**

## Components with Direct Fetch Calls

### Pages (Main Application Pages)
1. **`/src/app/llm-providers/page.tsx`**
   - `/api/providers` (GET) - line 72
   - `/api/providers/${providerId}/test` (POST) - line 88
   - `/api/providers/${providerId}` (DELETE) - line 121

2. **`/src/app/virtualkeys/page.tsx`**
   - `/api/virtualkeys` (GET) - line 78
   - `/api/virtualkeys/${keyId}` (DELETE) - line 134

3. **`/src/app/model-mappings/page.tsx`**
   - `/api/model-mappings` (GET) - line 78
   - `/api/model-mappings/${mappingId}/test` (POST) - line 94
   - `/api/model-mappings/${mappingId}` (DELETE) - line 124
   - `/api/model-mappings/discover` (POST) - line 148

4. **`/src/app/configuration/page.tsx`**
   - `/api/settings/system-info` (GET) - line 71 (commented out)
   - `/api/settings` (GET) - line 96 (commented out)
   - `/api/settings/${key}` (PUT) - line 125 (commented out)
   - Note: Currently broken due to SDK not being available

5. **`/src/app/usage-analytics/page.tsx`**
   - `/api/usage-analytics?range=${timeRange}` (GET) - line 104
   - `/api/usage-analytics/export?range=${timeRange}` (GET) - line 139

6. **`/src/app/system-info/page.tsx`**
   - Direct fetch calls (needs investigation)

7. **`/src/app/audio-providers/page.tsx`**
   - Direct fetch calls (needs investigation)

8. **`/src/app/request-logs/page.tsx`**
   - Direct fetch calls (needs investigation)

9. **`/src/app/login/page.tsx`**
   - Direct fetch calls (needs investigation)

10. **`/src/app/health-monitoring/page.tsx`**
    - Direct fetch calls (needs investigation)

11. **`/src/app/provider-health/page.tsx`**
    - Direct fetch calls (needs investigation)

12. **`/src/app/system-performance/page.tsx`**
    - Direct fetch calls (needs investigation)

13. **`/src/app/caching-settings/page.tsx`**
    - Direct fetch calls (needs investigation)

14. **`/src/app/virtual-keys-analytics/page.tsx`**
    - Direct fetch calls (needs investigation)

### Modal Components
15. **`/src/components/providers/CreateProviderModal.tsx`**
    - `/api/providers` (POST) - line 85
    - `/api/providers/test` (POST) - line 128

16. **`/src/components/providers/EditProviderModal.tsx`**
    - `/api/providers/${provider.id}` (PUT) - line 108

17. **`/src/components/virtualkeys/CreateVirtualKeyModal.tsx`**
    - Direct fetch calls (needs investigation)

18. **`/src/components/virtualkeys/EditVirtualKeyModal.tsx`**
    - Direct fetch calls (needs investigation)

19. **`/src/components/modelmappings/CreateModelMappingModal.tsx`**
    - `/api/providers` (GET) - line 101
    - `/api/model-mappings` (POST) - line 141

20. **`/src/components/modelmappings/EditModelMappingModal.tsx`**
    - `/api/model-mappings/${mapping.id}` (PUT) - line 64

### Custom Hooks
21. **`/src/hooks/useProviderApi.ts`**
    - `/api/providers` (GET) - line 59
    - `/api/providers/${id}` (GET) - line 84
    - `/api/providers` (POST) - line 109
    - `/api/providers/${id}` (PATCH) - line 149
    - `/api/providers/${id}` (DELETE) - line 189
    - `/api/providers/test` (POST) - line 222
    - `/api/health/providers/${id}` (GET) - line 262
    - `/api/providers/${id}/models` (GET) - line 287

22. **`/src/hooks/useMonitoringApi.ts`**
    - `/api/monitoring/metrics` (GET) - line 68
    - `/api/monitoring/health` (GET) - line 89
    - `/api/monitoring/alerts?${queryParams}` (GET) - line 117
    - `/api/monitoring/alerts/${alertId}/resolve` (POST) - line 138

23. **`/src/hooks/useSecurityApi.ts`**
    - `/api/admin/security/events?${queryParams}` (GET) - line 54
    - `/api/admin/security/threats?${queryParams}` (GET) - line 86
    - `/api/admin/security/ip-rules` (GET) - line 111
    - `/api/admin/security/ip-rules` (POST) - line 136
    - `/api/admin/security/ip-rules/${id}` (PUT) - line 176
    - `/api/admin/security/ip-rules/${id}` (DELETE) - line 216

24. **`/src/hooks/useConfigurationApi.ts`**
    - `/api/config/routing` (GET) - line 40
    - `/api/config/routing` (PUT) - line 65
    - `/api/config/caching` (GET) - line 105
    - `/api/config/caching` (PUT) - line 130

25. **`/src/hooks/useAuthApi.ts`**
    - `/api/auth/login` (POST) - line 31
    - `/api/auth/logout` (POST) - line 71
    - `/api/auth/validate` (GET) - line 105
    - `/api/auth/refresh` (POST) - line 130
    - `/api/auth/virtual-key` (GET) - line 151

26. **`/src/hooks/useCoreApi.ts`**
    - `/api/images/generate` (POST) - line 44
    - `/api/videos/generate` (POST) - line 84
    - `/api/audio/transcribe` (POST) - line 130
    - `/api/audio/speech` (POST) - line 167
    - `/api/chat/completions` (POST) - line 208

27. **`/src/hooks/useSystemApi.ts`**
    - `/api/admin/system/info` (GET) - line 59
    - `/api/admin/system/settings` (GET) - line 84
    - `/api/admin/system/settings` (PUT) - line 109
    - `/api/admin/system/health` (GET) - line 149
    - `/api/admin/system/services/${serviceName}/restart` (POST) - line 174
    - `/api/admin/system/backup` (POST) - line 207
    - `/api/admin/system/backups` (GET) - line 243
    - `/api/admin/system/backups/${backupId}/restore` (POST) - line 268

28. **`/src/hooks/useExportApi.ts`**
    - `/api/export/status/${exportId}` (GET) - line 80
    - `/api/admin/analytics/export` (POST) - line 147

29. **`/src/hooks/useBackendHealth.ts`**
    - `/api/admin/system/health` (GET) - line 34

30. **`/src/hooks/useSessionRefresh.ts`**
    - `/api/auth/refresh` (POST) - line 17

31. **`/src/hooks/useTableData.ts`**
    - Direct fetch calls (needs investigation)

### Utility Files
32. **`/src/lib/auth/validation.ts`**
    - Direct fetch calls (needs investigation)

33. **`/src/lib/utils/fetch-wrapper.ts`**
    - Base fetch wrapper implementation

34. **`/src/lib/server/adminClient.ts`**
    - Server-side admin client implementation

35. **`/src/lib/server/coreClient.ts`**
    - Server-side core client implementation

### API Route Handlers
36. **`/src/app/api/admin/[...path]/route.ts`**
    - Proxy handler for admin API

37-42. Various API route files in `/src/app/api/admin/model-mappings/`

## Patterns Observed

1. **Direct API calls to internal routes**: Most components call `/api/*` endpoints directly
2. **No SDK usage**: Despite the issue mentioning SDK migration, no SDK imports were found
3. **Inconsistent error handling**: Each component implements its own error handling
4. **Code duplication**: Similar fetch patterns repeated across components
5. **Some functionality broken**: Configuration page has commented out fetch calls with note "SDK not available"

## Recommended Migration Strategy

1. **Create SDK clients** for:
   - Admin API (`@conduit/admin-sdk`)
   - Core API (`@conduit/core-sdk`)

2. **Replace direct fetch calls** with SDK methods:
   - Provider management
   - Virtual key management
   - Model mapping management
   - System configuration
   - Analytics and monitoring
   - Authentication

3. **Standardize error handling** through SDK
4. **Remove duplicated code** by using shared SDK methods

## Priority Migration Order

1. **High Priority** (Core functionality):
   - Authentication hooks (`useAuthApi`)
   - Provider management (`useProviderApi`)
   - Virtual key management (pages and modals)

2. **Medium Priority** (Admin features):
   - System configuration (`useSystemApi`, `useConfigurationApi`)
   - Model mappings
   - Analytics pages

3. **Low Priority** (Supporting features):
   - Export functionality
   - Health monitoring
   - Security management