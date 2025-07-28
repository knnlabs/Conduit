# Provider Validation HTTP Client Fix

## Issue Description

When validating LLM provider credentials through the WebUI, users encountered the error:
```
Authentication verification failed: An invalid request URI was provided. Either the request URI must be an absolute URI or BaseAddress must be set.
```

This affected all provider types, not just OpenAI.

## Root Cause

The issue occurred because:

1. Provider clients were creating HttpClients with BaseAddress set (for relative URL operations)
2. During authentication verification, they were using absolute URLs returned by `GetHealthCheckUrl()`
3. The HttpClient configuration was inconsistent between regular operations and authentication verification
4. The `CreateTestClient` flow didn't properly initialize the BaseUrl property during validation

## Solution

### 1. Created a Dedicated Authentication Verification HttpClient Method

Added `CreateAuthenticationVerificationClient()` to `BaseLLMClient`:
- Creates HttpClient WITHOUT BaseAddress (since we use absolute URLs)
- Configures standard headers and authentication
- Uses a shorter timeout appropriate for health checks

### 2. Updated Provider Implementations

Modified all providers that implement `VerifyAuthenticationAsync` to use the new method:
- OpenAI Client
- Azure OpenAI Client  
- Other affected providers

### 3. Added Comprehensive Test Coverage

Created `ProviderAuthenticationTests.cs` with tests for:
- Valid API key scenarios
- Invalid API key scenarios
- Null BaseUrl handling
- Custom BaseUrl handling
- Factory test client creation

## Architecture Improvements

This fix:
- ✅ **No technical debt** - Addresses the root cause properly
- ✅ **Clear separation** - Authentication verification has its own HttpClient configuration
- ✅ **Consistent behavior** - All providers now handle URLs consistently
- ✅ **Better testability** - Comprehensive test coverage ensures no regressions
- ✅ **Improved debugging** - Clear distinction between regular and auth verification clients

## Testing

Run the provider authentication tests:
```bash
dotnet test --filter "FullyQualifiedName~ProviderAuthenticationTests"
```

Run integration tests:
```bash
dotnet test --filter "FullyQualifiedName~ProviderValidationIntegrationTests"
```

## Future Considerations

1. Consider migrating all providers to use either:
   - Absolute URLs everywhere (no BaseAddress)
   - OR relative URLs everywhere (always set BaseAddress)

2. Add more integration tests for each provider type

3. Consider creating provider-specific test suites for authentication verification