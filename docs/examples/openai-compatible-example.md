# Using OpenAI-Compatible Providers with ConduitLLM

ConduitLLM supports connecting to any service that implements the OpenAI API format. This includes services like:

- LocalAI
- Ollama (with OpenAI compatibility mode)
- Text Generation WebUI with OpenAI extension
- Self-hosted models with OpenAI-compatible wrappers
- Any other service implementing the OpenAI API specification

## Configuration

### Via WebUI

1. Navigate to the Configuration page
2. Click "Add Provider" and select "OpenAI Compatible"
3. Enter the following information:
   - **Provider Name**: OpenAI Compatible (pre-selected)
   - **API Base URL**: (Required) The base URL of your OpenAI-compatible service
     - Example: `http://localhost:5001/v1`
     - Example: `https://my-api-server.com/openai`
   - **API Key**: Your API key (if required by the service)
4. Test the connection and save

### Via Configuration File

Add the following to your `appsettings.json`:

```json
{
  "ConduitSettings": {
    "ProviderCredentials": [
      {
        "ProviderName": "openai-compatible",
        "ApiKey": "your-api-key-if-required",
        "ApiBase": "http://localhost:5001/v1"
      }
    ],
    "ModelProviderMappings": [
      {
        "ModelAlias": "local-llama",
        "ProviderName": "openai-compatible",
        "ProviderModelName": "llama2-7b"
      }
    ]
  }
}
```

## Usage Example

Once configured, you can use the OpenAI-compatible provider just like any other provider:

```csharp
using ConduitLLM.Core;

// Initialize Conduit
var conduit = new Conduit();

// Create a chat completion request
var request = new ChatCompletionRequest
{
    Model = "local-llama", // Your model alias
    Messages = new List<Message>
    {
        new Message { Role = "system", Content = "You are a helpful assistant." },
        new Message { Role = "user", Content = "Hello! How are you?" }
    }
};

// Get the response
var response = await conduit.GetChatCompletionAsync(request);
Console.WriteLine(response.Choices[0].Message.Content);
```

## Common Use Cases

### LocalAI
```json
{
  "ProviderName": "openai-compatible",
  "ApiBase": "http://localhost:8080/v1",
  "ApiKey": "not-required"  // LocalAI typically doesn't require an API key
}
```

### Ollama with OpenAI Compatibility
```json
{
  "ProviderName": "openai-compatible",
  "ApiBase": "http://localhost:11434/v1",
  "ApiKey": "ollama"  // Ollama accepts any non-empty string
}
```

### Text Generation WebUI
```json
{
  "ProviderName": "openai-compatible",
  "ApiBase": "http://localhost:5000/v1",
  "ApiKey": "your-api-key"
}
```

## Troubleshooting

1. **Connection Test Fails**
   - Verify the API base URL is correct and includes the version (usually `/v1`)
   - Check if the service is running and accessible
   - Ensure any required API key is provided

2. **Model Not Found**
   - Use the model ID exactly as expected by your service
   - Some services require specific model naming conventions

3. **Authentication Errors**
   - Some services don't require API keys - try using a placeholder like "not-required"
   - Others may require specific API key formats

## Notes

- The OpenAI Compatible provider uses standard Bearer token authentication
- All OpenAI API features are supported (chat, embeddings, images) if your service implements them
- Response formats should match the OpenAI API specification for compatibility