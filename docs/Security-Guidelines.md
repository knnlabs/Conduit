# Security Guidelines

This document provides security guidelines and best practices for the Conduit project.

## CodeQL Security Analysis

### Fixing Log Injection Vulnerabilities (CWE-117)

CodeQL detects log injection vulnerabilities when user input is logged without proper sanitization. This can allow attackers to forge log entries or inject malicious content into logs.

#### The Problem with Custom Sanitizers

CodeQL's default configuration does **not** recognize custom sanitizer wrapper functions. This means that even if you have a secure sanitization function, CodeQL will still flag the code as vulnerable.

```csharp
// This custom sanitizer is NOT recognized by CodeQL
public static string S(object value) 
{
    return value?.ToString()?.Replace(Environment.NewLine, "") ?? "";
}

// CodeQL will still flag this as vulnerable
_logger.LogInformation("Processing user {Name}", S(userName));
```

#### The Solution: Inline Sanitization

CodeQL **does** recognize inline string replacement operations. To fix log injection alerts:

1. **For String Parameters**: Use inline `.Replace(Environment.NewLine, "")`
   ```csharp
   // CodeQL recognizes this pattern
   _logger.LogInformation("Processing user {Name}", userName.Replace(Environment.NewLine, ""));
   ```

2. **For Nullable Strings**: Use null-safe operators
   ```csharp
   // Handle nullable strings safely
   _logger.LogInformation("Processing {Name}", userName?.Replace(Environment.NewLine, "") ?? "unknown");
   ```

3. **For Non-String Types**: No sanitization needed
   ```csharp
   // Integers, GUIDs, DateTimes, etc. don't need sanitization
   _logger.LogInformation("Processing ID {Id}", userId);  // userId is int
   ```

#### What to Sanitize

**DO Sanitize:**
- User input from HTTP requests (route parameters, query strings, form data)
- Data from external APIs
- File names and paths from user uploads
- Any string that originates from untrusted sources

**DON'T Sanitize:**
- System-generated IDs (integers, GUIDs)
- Timestamps and dates
- Numeric values (counts, metrics, calculations)
- Enum values
- Boolean values

#### Verification

After applying fixes, verify with CodeQL:

```bash
# Create CodeQL database
codeql database create conduit-codeql-db --language=csharp --command='dotnet build'

# Run security queries
codeql database analyze conduit-codeql-db --format=csv --output=results.csv \
  codeql/csharp-queries:Security/CWE/CWE-117/LogForging.ql
```

A successful fix will show 0 results in the CSV file.

## Other Security Considerations

### API Key Security
- Virtual keys are hashed before storage using SHA256
- Never log full API keys, only prefixes
- Use secure random generation for key creation

### Input Validation
- Validate all user input at API boundaries
- Use data annotations for model validation
- Sanitize file names and paths

### Authentication & Authorization
- Admin endpoints require master key authentication
- Virtual keys have configurable permissions and rate limits
- IP filtering available for additional security

### Secure Communication
- Always use HTTPS in production
- Validate SSL certificates for external API calls
- Use secure WebSocket connections for real-time features