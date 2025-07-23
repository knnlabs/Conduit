# Security Guidelines

This document provides security guidelines and best practices for the Conduit project.

## Secret Detection and Pre-commit Hooks

Conduit uses automated secret detection to prevent API keys, passwords, and other sensitive information from being committed to the repository. 

For setup instructions and detailed information, see [Security Pre-commit Hooks](./Security-Pre-commit-Hooks.md).

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

### Unicode Line/Paragraph Separator Sanitization

The `LoggingSanitizer` class includes protection against Unicode line separator (U+2028) and paragraph separator (U+2029) characters. These characters can be interpreted as line breaks by some systems, potentially allowing log injection attacks.

#### The Security Fix

The sanitizer removes these Unicode separators along with standard CRLF characters:

```csharp
// Unicode separators that could be interpreted as newlines
private static readonly Regex UnicodeSeparatorPattern = new(@"[\u2028\u2029]", RegexOptions.Compiled);

// In the sanitization method
str = UnicodeSeparatorPattern.Replace(str, " ");
```

#### Why This Matters

- **U+2028 (LINE SEPARATOR)** and **U+2029 (PARAGRAPH SEPARATOR)** are not included in standard ASCII control character ranges
- Some log parsers and analysis tools interpret these as line breaks
- Attackers could use these to inject fake log entries or corrupt structured log formats
- This fix prevents log injection attacks that bypass traditional CRLF sanitization

#### Using the Sanitizer

Always use `LoggingSanitizer.S()` for user-controlled data:

```csharp
_logger.LogInformation("User input: {Input}", LoggingSanitizer.S(userInput));
```

## Other Security Considerations

### Insecure Mode Protection

ConduitLLM includes an "insecure mode" (`CONDUIT_INSECURE=true`) that bypasses authentication for development purposes. This mode has strict security controls:

#### Environment Restrictions
- **Development Only**: Insecure mode can ONLY be enabled in development environments
- **Automatic Validation**: The application validates the environment at startup
- **Hard Failure**: If insecure mode is detected in Production or Staging environments, the application will:
  - Throw an `InvalidOperationException`
  - Display an error message: "SECURITY VIOLATION: Insecure mode cannot be enabled in [environment]"
  - Refuse to start until the environment variable is removed

#### Security Indicators
When insecure mode is active in development:
- **Console Warnings**: Prominent warnings are displayed at startup with emoji indicators
- **UI Banner**: A yellow warning banner appears at the top of all pages
- **Log Warnings**: Warning-level logs are written throughout the application lifecycle

#### Best Practices
- **Never** set `CONDUIT_INSECURE=true` in production environments
- Use proper authentication keys (`CONDUIT_WEBUI_AUTH_KEY` or `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`) for all non-development deployments
- Regularly audit environment variables in deployment pipelines
- Consider using separate configuration files for development vs production

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