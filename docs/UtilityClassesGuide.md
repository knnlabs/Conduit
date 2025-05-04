# Conduit Utility Classes Guide

This guide provides examples of how to use the utility classes that have been created to reduce code duplication and standardize common operations across the Conduit codebase.

## Table of Contents

1. [StreamHelper](#streamhelper)
2. [ExceptionHandler](#exceptionhandler)
3. [BaseLLMClient](#basellmclient)
4. [OpenAICompatibleClient](#openaicompatibleclient) 
5. [FileHelper](#filehelper)
6. [ValidationHelper](#validationhelper)

## StreamHelper

The `StreamHelper` class provides utilities for processing streaming responses from LLM providers, particularly server-sent events (SSE).

### Example: Processing an SSE Stream

```csharp
using ConduitLLM.Core.Utilities;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// Inside your method
var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

// Process a standard SSE stream
await foreach (var chunk in StreamHelper.ProcessSseStreamAsync<YourResponseType>(
    httpResponse, logger, jsonOptions, cancellationToken))
{
    // Handle each chunk from the stream
    yield return chunk;
}
```

### Example: Transforming a Stream

```csharp
using ConduitLLM.Core.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

public async IAsyncEnumerable<StandardChunkType> GetStandardizedStreamAsync(
    IAsyncEnumerable<ProviderChunkType> providerStream,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var chunk in StreamHelper.TransformStreamAsync(
        providerStream, 
        MapToStandardChunk, 
        cancellationToken))
    {
        yield return chunk;
    }
}

private StandardChunkType MapToStandardChunk(ProviderChunkType chunk)
{
    // Mapping logic here
    return new StandardChunkType { /* ... */ };
}
```

## ExceptionHandler

The `ExceptionHandler` class provides standardized error handling for operations, especially HTTP requests.

### Example: Handling HTTP Requests with Error Translation

```csharp
using ConduitLLM.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

public async Task<ApiResponse> FetchDataAsync(string resourceId)
{
    return await ExceptionHandler.HandleHttpRequestAsync(
        async () => {
            using var client = new HttpClient();
            var response = await client.GetAsync($"https://api.example.com/resources/{resourceId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<ApiResponse>();
        },
        _logger,
        "ExampleService");
}
```

### Example: Custom Operation with Error Handling

```csharp
using ConduitLLM.Core.Utilities;
using ConduitLLM.Core.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public async Task<ProcessingResult> ProcessDataAsync(InputData data)
{
    return await ExceptionHandler.ExecuteWithErrorHandlingAsync(
        async () => {
            // Your processing logic here
            return await _processor.ProcessAsync(data);
        },
        _logger,
        "Error processing data",
        (ex) => {
            // Custom exception transformer
            if (ex is TimeoutException)
            {
                return new ProcessingException("Processing timed out", ex);
            }
            return new ProcessingException("Processing failed", ex);
        });
}
```

## BaseLLMClient

The `BaseLLMClient` abstract class provides a common foundation for implementing LLM provider clients.

### Example: Creating a New Provider Client

```csharp
using ConduitLLM.Providers;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class NewProviderClient : BaseLLMClient
{
    private readonly string _apiUrl;

    public NewProviderClient(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<NewProviderClient> logger,
        IHttpClientFactory httpClientFactory = null)
        : base(credentials, providerModelId, logger, httpClientFactory, "NewProvider")
    {
        _apiUrl = "https://api.newprovider.com/v1";
    }

    protected override void ConfigureHttpClient(HttpClient client, string apiKey)
    {
        base.ConfigureHttpClient(client, apiKey);
        // Add provider-specific headers or settings
        client.DefaultRequestHeaders.Add("X-Provider-Version", "1.0");
    }

    public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, "ChatCompletion");
        
        return await ExecuteApiRequestAsync(async () => {
            // Implementation details using common utilities
            using var client = CreateHttpClient(apiKey);
            
            // Map request to provider format
            // Send request and get response
            // Map response to standard format
            
            return new ChatCompletionResponse { /* Populated from response */ };
        }, "ChatCompletion", cancellationToken);
    }

    // Implement other required methods...
}
```

## OpenAICompatibleClient

The `OpenAICompatibleClient` class provides a base implementation for providers with OpenAI-compatible APIs.

### Example: Creating an OpenAI-Compatible Provider

```csharp
using ConduitLLM.Providers;
using Microsoft.Extensions.Logging;
using System.Net.Http;

public class CompatibleProviderClient : OpenAICompatibleClient
{
    public CompatibleProviderClient(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<CompatibleProviderClient> logger,
        IHttpClientFactory httpClientFactory = null)
        : base(credentials, providerModelId, logger, httpClientFactory, "CompatibleProvider", "https://api.compatible-provider.com/v1")
    {
    }
    
    // Most functionality is inherited from OpenAICompatibleClient
    
    // Override any provider-specific endpoints or behavior
    protected override string GetChatCompletionEndpoint()
    {
        return $"{BaseUrl}/completions"; // If different from OpenAI standard
    }
    
    protected override void ConfigureHttpClient(HttpClient client, string apiKey)
    {
        base.ConfigureHttpClient(client, apiKey);
        // Add provider-specific configuration
        client.DefaultRequestHeaders.Add("X-Custom-Header", "Value");
    }
}
```

## FileHelper

The `FileHelper` class provides utilities for common file operations with standardized error handling.

### Example: Reading and Writing JSON Files

```csharp
using ConduitLLM.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

public async Task SaveConfigurationAsync(AppConfiguration config)
{
    await FileHelper.WriteJsonFileAsync(
        "/path/to/config.json",
        config,
        null, // Use default options
        _logger);
}

public async Task<AppConfiguration?> LoadConfigurationAsync()
{
    return await FileHelper.ReadJsonFileAsync<AppConfiguration>(
        "/path/to/config.json",
        null, // Use default options
        _logger);
}
```

### Example: Reading and Writing Text Files

```csharp
using ConduitLLM.Core.Utilities;
using System.Threading.Tasks;

public async Task SaveLogAsync(string logContent)
{
    await FileHelper.WriteTextFileAsync(
        "/path/to/logs/app.log", 
        logContent, 
        _logger);
}

public async Task<string?> LoadTemplateAsync()
{
    return await FileHelper.ReadTextFileAsync(
        "/path/to/templates/email.html", 
        _logger);
}
```

## ValidationHelper

The `ValidationHelper` class provides methods for common validation operations.

### Example: Request Validation

```csharp
using ConduitLLM.Core.Utilities;
using ConduitLLM.Core.Exceptions;

public void ValidateRequest(RequestDto request)
{
    try
    {
        // Basic validation
        ValidationHelper.RequireNotNull(request, nameof(request));
        ValidationHelper.RequireNonEmpty(request.Id, "request.Id");
        
        // Range validation
        ValidationHelper.RequireRange(request.Count, 1, 100, "request.Count");
        
        // Pattern validation
        ValidationHelper.RequirePattern(
            request.Email, 
            @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", 
            "request.Email", 
            "valid email format");
        
        // Date validation
        ValidationHelper.RequireNotFutureDate(request.CreatedDate, "request.CreatedDate");
        
        // Numeric validation
        ValidationHelper.RequirePositive(request.Amount, "request.Amount");
        
        // Custom condition
        ValidationHelper.RequireCondition(
            request.EndDate > request.StartDate,
            "End date must be after start date");
    }
    catch (ValidationException ex)
    {
        // Handle validation errors
        throw new BadRequestException(ex.Message, ex);
    }
}
```

### Example: Entity Validation

```csharp
using ConduitLLM.Core.Utilities;

public class EntityValidator<T> where T : BaseEntity
{
    public bool Validate(T entity, out string errorMessage)
    {
        errorMessage = string.Empty;
        
        try
        {
            ValidationHelper.RequireValidGuid(entity.Id, "Id");
            ValidationHelper.RequireNonEmpty(entity.Name, "Name");
            ValidationHelper.RequireMaxLength(entity.Description, 500, "Description");
            
            if (entity is UserEntity user)
            {
                ValidationHelper.RequireNonEmpty(user.Email, "Email");
                ValidationHelper.RequirePattern(
                    user.Email, 
                    @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", 
                    "Email");
            }
            
            return true;
        }
        catch (ValidationException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
```

## Combining Utilities

These utility classes work well together to create clean, maintainable code:

```csharp
using ConduitLLM.Core.Utilities;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public async Task<ProcessingResult> ProcessRequestAsync(RequestData request, CancellationToken cancellationToken)
{
    // Validate the request
    ValidationHelper.RequireNotNull(request, nameof(request));
    ValidationHelper.RequireNonEmpty(request.ModelId, "request.ModelId");
    
    // Process with error handling
    return await ExceptionHandler.ExecuteWithErrorHandlingAsync(
        async () => {
            // Load configuration
            var config = await FileHelper.ReadJsonFileAsync<AppConfiguration>(
                "/path/to/config.json", 
                null,
                _logger,
                cancellationToken);
                
            // Process the request and return results
            return await _processor.ProcessAsync(request, config, cancellationToken);
        },
        _logger,
        "Error processing request");
}
```

## Best Practices

1. **Use Batch Operations**: When making multiple file operations, use a batch approach to improve performance.

2. **Error Handling**: Always catch and properly handle exceptions from utility methods.

3. **Logging**: Pass a logger to utility methods when available to ensure proper error logging.

4. **Cancellation Support**: Pass cancellation tokens to async operations to support proper cancellation.

5. **Extension**: Extend the utility classes as needed, but maintain consistency with the established patterns.

6. **Validation First**: Perform validation early in your methods to fail fast and provide clear error messages.

7. **Resource Disposal**: Ensure proper disposal of resources, especially when dealing with streams and HTTP clients.

## Contributing

When extending these utility classes:

1. Maintain consistent method signatures and patterns
2. Add comprehensive XML documentation
3. Keep methods focused on a single responsibility
4. Add unit tests for new functionality
5. Update this guide with examples of new functionality