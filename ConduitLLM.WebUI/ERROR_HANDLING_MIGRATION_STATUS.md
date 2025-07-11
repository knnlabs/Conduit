# Error Handling Migration Status

This report identifies all API route files that need to be updated to use the standardized `handleSDKError` from `@/lib/errors/sdk-errors.ts`.

## Summary

- **Total API route files**: 61
- **Files with catch blocks not using handleSDKError**: 47
- **Files with no error handling**: 14
- **Files already using handleSDKError**: 0

## Files That Need Migration (47 files)

These files have catch blocks but are using custom error handling instead of `handleSDKError`:

### Authentication Routes
- `/app/api/auth/login/route.ts`
- `/app/api/auth/logout/route.ts`
- `/app/api/auth/refresh/route.ts`
- `/app/api/auth/validate/route.ts`
- `/app/api/auth/virtual-key/route.ts`

### Core API Routes
- `/app/api/chat/completions/route.ts`
- `/app/api/images/generate/route.ts`
- `/app/api/videos/generate/route.ts`
- `/app/api/audio/speech/route.ts`
- `/app/api/audio/transcribe/route.ts`

### Virtual Keys Routes
- `/app/api/virtualkeys/route.ts`
- `/app/api/virtualkeys/[id]/route.ts`

### Provider Routes
- `/app/api/providers/route.ts`
- `/app/api/providers/[id]/route.ts`
- `/app/api/providers/[id]/models/route.ts`
- `/app/api/providers/[id]/test/route.ts`
- `/app/api/providers/test/route.ts`

### Health & Monitoring Routes
- `/app/api/health/system/route.ts`
- `/app/api/health/events/route.ts`
- `/app/api/health/providers/route.ts`
- `/app/api/health/providers/[id]/route.ts`
- `/app/api/provider-health/route.ts`
- `/app/api/provider-health/export/route.ts`

### Analytics Routes
- `/app/api/usage-analytics/route.ts`
- `/app/api/usage-analytics/export/route.ts`
- `/app/api/virtual-keys-analytics/route.ts`
- `/app/api/virtual-keys-analytics/export/route.ts`
- `/app/api/system-performance/route.ts`
- `/app/api/system-performance/export/route.ts`

### Settings Routes
- `/app/api/settings/route.ts`
- `/app/api/settings/[key]/route.ts`
- `/app/api/settings/batch/route.ts`
- `/app/api/settings/system-info/route.ts`

### Request Logs Routes
- `/app/api/request-logs/route.ts`
- `/app/api/request-logs/export/route.ts`

### Model Mappings Routes
- `/app/api/model-mappings/route.ts`
- `/app/api/model-mappings/[id]/route.ts`
- `/app/api/model-mappings/[id]/test/route.ts`
- `/app/api/model-mappings/discover/route.ts`

### Admin Routes
- `/app/api/admin/[...path]/route.ts`
- `/app/api/admin/system/health/route.ts`
- `/app/api/admin/system/info/route.ts`
- `/app/api/admin/model-mappings/route.ts`
- `/app/api/admin/model-mappings/[id]/route.ts`
- `/app/api/admin/model-mappings/discover/route.ts`
- `/app/api/admin/model-mappings/test/route.ts`

### Export Status Route
- `/app/api/export/status/[exportId]/route.ts`

## Files Without Error Handling (14 files)

These files don't have any try-catch blocks and may need error handling added:

### Admin Routes
- `/app/api/admin/security/ip-rules/[id]/route.ts`
- `/app/api/admin/security/ip-rules/route.ts`
- `/app/api/admin/security/threats/route.ts`
- `/app/api/admin/security/events/route.ts`
- `/app/api/admin/analytics/export/route.ts`
- `/app/api/admin/system/settings/route.ts`
- `/app/api/admin/audio-configuration/route.ts`
- `/app/api/admin/audio-configuration/[providerId]/route.ts`
- `/app/api/admin/audio-configuration/[providerId]/test/route.ts`
- `/app/api/admin/request-logs/route.ts`

### Configuration Routes
- `/app/api/config/routing/route.ts`
- `/app/api/config/caching/route.ts`
- `/app/api/config/caching/[cacheId]/clear/route.ts`

### Health Route
- `/app/api/health/connections/route.ts`

## Migration Pattern

All files should be updated to follow this pattern:

```typescript
import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// or
import { getServerCoreClient } from '@/lib/server/coreClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    // SDK operations...
  } catch (error) {
    return handleSDKError(error);
  }
}
```

## Notes

1. The `handleSDKError` function properly maps SDK-specific errors to appropriate HTTP responses
2. It includes proper logging with `logger.error()`
3. It handles all SDK error types (AuthError, ValidationError, RateLimitError, etc.)
4. It also handles non-SDK errors like network errors (ECONNREFUSED, ETIMEDOUT)
5. The old `mapSDKErrorToResponse` is maintained as a legacy alias but should not be used