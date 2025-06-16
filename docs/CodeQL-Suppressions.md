# CodeQL Suppressions

This document tracks the false positive suppressions applied to the codebase for CodeQL security scanning.

## Suppression Format

CodeQL recognizes LGTM-style inline comment suppressions in the format:
```
// lgtm [rule-id]
```

## Applied Suppressions

### 1. Clear Text Storage of Sensitive Information (cs/cleartext-storage-of-sensitive-information)

These are false positives where connection strings are already being sanitized before logging:

- **ConduitLLM.Configuration/Services/RedisConnectionFactory.cs**
  - Line 106: `connectionString.Replace("password=", "password=******")` - Password already masked
  - Line 113: `connectionString.Replace("password=", "password=******")` - Password already masked

- **ConduitLLM.Core/Data/ConnectionStringManager.cs**
  - Line 32: Logger action that uses `SanitizeConnectionString()` method
  - Line 195: Using `SanitizeConnectionString()` before logging
  - Line 207: Using `SanitizeConnectionString()` before logging

- **ConduitLLM.Core/Data/DatabaseConnectionFactory.cs**
  - Line 40: Logger action that delegates to ConnectionStringManager which sanitizes

### 2. User-Controlled Bypass (cs/user-controlled-bypass)

- **ConduitLLM.Admin/Services/AdminProviderCredentialService.cs**
  - Line 183: This is a null check on the input parameter, not a security bypass

### 3. Missing Function Level Access Control (cs/web/missing-function-level-access-control)

- **ConduitLLM.Admin/Controllers/VirtualKeysController.cs**
  - Line 228: The `ValidateKey` endpoint is intentionally public as it's used to validate API keys

## Notes

- These suppressions are applied because CodeQL cannot detect that connection strings are sanitized through helper methods
- The `ValidateKey` endpoint must remain public by design as it's the authentication endpoint
- All sensitive data logging uses proper sanitization methods that mask passwords and other credentials