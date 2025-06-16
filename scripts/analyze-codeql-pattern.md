# CodeQL Log Injection Detection Analysis

## The Problem

CodeQL is detecting "Log entries created from user input" because it tracks data flow from:
1. **Sources**: User-controlled input (route parameters, query strings, request bodies)
2. **Sinks**: Logging statements (`ILogger.Log*` methods)

## Why Our Fixes Didn't Work

1. **Custom Extension Methods**: CodeQL doesn't understand that `LogErrorSecure`, `LogWarningSecure`, etc. are sanitizing the input because:
   - It doesn't follow the call chain through custom extension methods
   - The sanitization happens inside the extension method, not at the call site

2. **Wrapper Methods**: Even though `SecureLoggingExtensions` sanitizes input, CodeQL sees:
   ```csharp
   _logger.LogErrorSecure(ex, "Message {Param}", userInput);
   ```
   And internally this calls:
   ```csharp
   logger.LogError(ex, "Message {Param}", SanitizeArgs(args));
   ```
   CodeQL still sees the flow from `userInput` to `LogError`.

## What CodeQL Expects

CodeQL expects one of these patterns:

1. **Inline Sanitization at Call Site**:
   ```csharp
   _logger.LogError(ex, "Message {Param}", Sanitize(userInput));
   ```

2. **Built-in Safe Methods**: Using methods that CodeQL's queries recognize as safe.

3. **Data Flow Interruption**: Breaking the data flow between source and sink.

## The Real Solution

We need to use one of these approaches:

### Option 1: Inline Sanitization (Recommended)
```csharp
using static ConduitLLM.Core.Extensions.LoggingSanitizer;

// Then in your code:
_logger.LogError(ex, "Error for {Id}", S(id));
_logger.LogWarning("Invalid {Name}", S(request.Name));
```

### Option 2: CodeQL Suppression Comments
```csharp
// codeql[cs/log-injection] : False positive - input is sanitized
_logger.LogError(ex, "Error for {Id}", id);
```

### Option 3: Use Structured Logging Differently
Instead of logging user input directly, log only safe metadata:
```csharp
_logger.LogError(ex, "Provider credential not found");
// Don't include the ID in the log message
```

## Why Inline Sanitization Works

When CodeQL sees:
```csharp
_logger.LogError(ex, "Message {Param}", S(userInput));
```

It recognizes that `userInput` goes through a transformation function before reaching the logging sink. This breaks the direct data flow and satisfies the security check.

## Implementation Strategy

1. Add `using static ConduitLLM.Core.Extensions.LoggingSanitizer;` to files
2. Wrap all user-controlled parameters with `S()` at the call site
3. This includes:
   - Route parameters (id, name, etc.)
   - Query string values
   - Request body properties
   - Headers
   - Any external input

## Example Transformations

Before:
```csharp
_logger.LogError(ex, "Error getting provider {Name}", request.ProviderName);
_logger.LogWarning("Invalid key {Key}", virtualKey);
_logger.LogInformation("Processing request for {Id}", id);
```

After:
```csharp
_logger.LogError(ex, "Error getting provider {Name}", S(request.ProviderName));
_logger.LogWarning("Invalid key {Key}", S(virtualKey));
_logger.LogInformation("Processing request for {Id}", S(id));
```