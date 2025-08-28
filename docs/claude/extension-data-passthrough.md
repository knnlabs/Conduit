# ExtensionData Pass-Through Feature

## Overview

Conduit now supports passing arbitrary parameters to any LLM provider through the `ExtensionData` property. This feature enables support for the wide variety of parameters used by open-source models without requiring Conduit to maintain an exhaustive list of every possible parameter.

## Architecture

### Hybrid Approach

Conduit uses a hybrid approach that maintains type safety for essential routing parameters while allowing flexibility for model-specific parameters:

- **Explicit Properties**: Core parameters required for routing (`model`, `prompt`/`messages`)
- **ExtensionData**: All other parameters passed as `Dictionary<string, JsonElement>`

### Supported Request Types

The following request types now support ExtensionData:

1. **ChatCompletionRequest** - Text generation requests
2. **ImageGenerationRequest** - Image generation requests  
3. **VideoGenerationRequest** - Video generation requests

## How It Works

### 1. Client Request

Clients can include any additional parameters in their API requests:

```json
{
  "model": "stable-diffusion-xl",
  "prompt": "a beautiful landscape",
  "negative_prompt": "blurry, low quality",
  "seed": 42,
  "guidance_scale": 7.5,
  "num_inference_steps": 50,
  "sampler": "DPM++ 2M Karras"
}
```

### 2. Request Processing

The request is deserialized with:
- Standard properties mapped to explicit fields
- Unknown properties captured in `ExtensionData` via `[JsonExtensionData]` attribute

### 3. Validation

`MinimalParameterValidator` performs provider-agnostic validation:
- Validates only truly universal constraints
- Removes null/undefined values
- Logs warnings for obviously wrong values (e.g., negative tokens)
- Does NOT enforce provider-specific limits

### 4. Provider Pass-Through

Providers merge ExtensionData into their API requests:

```csharp
// Create request dictionary with standard parameters
var providerRequest = new Dictionary<string, object?>
{
    ["prompt"] = request.Prompt,
    ["model"] = request.Model
};

// Pass through ExtensionData
if (request.ExtensionData != null)
{
    foreach (var kvp in request.ExtensionData)
    {
        if (!providerRequest.ContainsKey(kvp.Key))
        {
            providerRequest[kvp.Key] = kvp.Value;
        }
    }
}
```

## Supported Data Types

ExtensionData supports any JSON-serializable type:

- **Numbers**: integers, decimals, floats
- **Strings**: text values, base64 encoded files
- **Booleans**: true/false
- **Arrays**: lists of values
- **Objects**: nested structures
- **Null**: explicit null values

## Examples

### Text Generation with Custom Parameters

```json
{
  "model": "mistral-7b",
  "messages": [{"role": "user", "content": "Hello"}],
  "temperature": 0.7,
  "top_k": 40,
  "repetition_penalty": 1.1,
  "mirostat": 2,
  "mirostat_tau": 5.0
}
```

### Image Generation with Stable Diffusion Parameters

```json
{
  "model": "stable-diffusion-xl",
  "prompt": "cyberpunk city",
  "negative_prompt": "blurry, distorted",
  "width": 1024,
  "height": 1024,
  "seed": 42,
  "guidance_scale": 7.5,
  "num_inference_steps": 30,
  "sampler": "DPM++ 2M Karras",
  "loras": ["style_lora_v1", "detail_enhancer"],
  "lora_weights": [0.8, 0.5]
}
```

### Video Generation with Motion Controls

```json
{
  "model": "stable-video-diffusion",
  "prompt": "timelapse of clouds",
  "negative_prompt": "static, blurry",
  "seed": 12345,
  "motion_bucket_id": 127,
  "decode_chunk_size": 8,
  "guidance_scale": 3.0,
  "num_frames": 25
}
```

### Image-to-Image with ControlNet

```json
{
  "model": "controlnet-model",
  "prompt": "detailed architectural drawing",
  "init_image": "data:image/png;base64,iVBORw0KGgo...",
  "controlnet": {
    "model": "openpose",
    "strength": 0.8,
    "guidance_start": 0.0,
    "guidance_end": 1.0
  }
}
```

## Provider Support

### Currently Supporting ExtensionData

- **OpenAICompatible** - Full support for text, image, and video
- **MiniMax** - Video generation with ExtensionData

### Planned Support

- Replicate
- Fireworks
- Other providers as needed

## Benefits

1. **Future-Proof**: No code changes needed when models add new parameters
2. **Flexibility**: Support any parameter any model might need
3. **Type Safety**: Core routing parameters remain strongly typed
4. **Backward Compatible**: Existing code continues to work
5. **Provider Agnostic**: Works with any provider that accepts JSON requests

## Best Practices

### For API Consumers

1. Include model-specific parameters directly in the request
2. Use appropriate data types (numbers as numbers, not strings)
3. Refer to model documentation for supported parameters
4. Test with small requests first to verify parameter support

### For Provider Implementers

1. Always check ExtensionData for additional parameters
2. Don't override standard parameters with ExtensionData
3. Pass through parameters even if not recognized (let provider decide)
4. Log warnings for problematic values but don't block requests

## Limitations

1. No compile-time checking of extension parameters
2. No IntelliSense/autocomplete for extension parameters
3. Validation is minimal and provider-agnostic
4. Errors from invalid parameters come from providers, not Conduit

## Migration Guide

### For Existing Code

No changes required! Existing code that uses only standard parameters will continue to work exactly as before.

### For New Features

Instead of adding new explicit properties for every model parameter:

**Before** (would require code changes):
```csharp
public class ImageGenerationRequest
{
    public string NegativePrompt { get; set; }
    public int? Seed { get; set; }
    public double? GuidanceScale { get; set; }
    // ... endless parameters for every model
}
```

**After** (no code changes needed):
```csharp
// Just pass parameters in the request
{
  "negative_prompt": "blurry",
  "seed": 42,
  "guidance_scale": 7.5
  // ... any parameters the model supports
}
```

## Technical Implementation

### Request Models

```csharp
public class ImageGenerationRequest
{
    // Core parameters for routing
    public required string Model { get; set; }
    public required string Prompt { get; set; }
    
    // Standard parameters
    public int N { get; set; } = 1;
    public string? Size { get; set; }
    
    // Extension data for model-specific parameters
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
```

### Validation

```csharp
public class MinimalParameterValidator
{
    public void ValidateImageParameters(ImageGenerationRequest request)
    {
        // Only validate obvious errors
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ArgumentException("Model is required");
            
        if (request.N < 1)
            request.N = 1;
            
        // Clean extension data
        CleanExtensionData(request.ExtensionData);
    }
}
```

### Provider Implementation

```csharp
public async Task<ImageGenerationResponse> CreateImageAsync(
    ImageGenerationRequest request)
{
    var apiRequest = new Dictionary<string, object?>
    {
        ["prompt"] = request.Prompt,
        ["n"] = request.N
    };
    
    // Pass through extension data
    if (request.ExtensionData != null)
    {
        foreach (var kvp in request.ExtensionData)
        {
            if (!apiRequest.ContainsKey(kvp.Key))
                apiRequest[kvp.Key] = kvp.Value;
        }
    }
    
    return await SendRequestAsync(apiRequest);
}
```

## Summary

The ExtensionData feature enables Conduit to support the full parameter space of open-source models without maintaining model-specific code. This approach provides maximum flexibility while maintaining type safety for core routing functionality.