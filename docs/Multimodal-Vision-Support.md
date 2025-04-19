# Multimodal (Vision) Support in ConduitLLM

This document describes how to use the multimodal (vision) support in ConduitLLM, which allows you to include images in your chat completions requests.

## Overview

ConduitLLM now supports multimodal content in messages, which means you can send both text and images in a single request. This is compatible with OpenAI's vision API format, allowing you to use vision-enabled models like GPT-4 Vision.

## Message Content Format

The `Message.Content` property now supports two formats:

1. **String format** (backward compatible): A simple string containing only text.
   ```json
   {
     "role": "user",
     "content": "Hello, world!"
   }
   ```

2. **Array format** (for multimodal content): An array of content parts, where each part can be either text or an image.
   ```json
   {
     "role": "user",
     "content": [
       {
         "type": "text",
         "text": "What's in this image?"
       },
       {
         "type": "image_url",
         "image_url": {
           "url": "https://example.com/image.jpg",
           "detail": "high"
         }
       }
     ]
   }
   ```

## Content Part Types

### Text Content Part

A text content part represents a text segment in a multimodal message.

```json
{
  "type": "text",
  "text": "This is text content"
}
```

### Image URL Content Part

An image URL content part represents an image in a multimodal message.

```json
{
  "type": "image_url",
  "image_url": {
    "url": "https://example.com/image.jpg",
    "detail": "high"
  }
}
```

**Parameters:**

- `url`: Required. The URL of the image. Can be either:
  - An HTTP URL: `https://example.com/image.jpg`
  - A data URL with base64-encoded image: `data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAS...`

- `detail`: Optional. The level of detail requested for the image analysis.
  - `"high"`: High level of detail, more tokens used
  - `"low"`: Low level of detail, fewer tokens used
  - `"auto"`: (Default) Let the model decide the appropriate level of detail

## Using Multimodal Content in C#

### Creating a Message with Text and Image

```csharp
using ConduitLLM.Core.Models;

// Create a message with text and image content
var message = new Message
{
    Role = "user",
    Content = new List<object>
    {
        new TextContentPart
        {
            Text = "What's in this image?"
        },
        new ImageUrlContentPart
        {
            ImageUrl = new ImageUrl
            {
                Url = "https://example.com/image.jpg",
                Detail = "high"
            }
        }
    }
};
```

### Creating a Chat Completion Request with Multimodal Content

```csharp
// Create the chat completion request
var request = new ChatCompletionRequest
{
    Model = "gpt-4-vision", // Use a model with vision capabilities
    Messages = new List<Message>
    {
        // System message (text only)
        new Message
        {
            Role = "system",
            Content = "You are a helpful assistant skilled in analyzing images."
        },
        
        // User message with text and image
        new Message
        {
            Role = "user",
            Content = new List<object>
            {
                new TextContentPart
                {
                    Text = "What's in this image?"
                },
                new ImageUrlContentPart
                {
                    ImageUrl = new ImageUrl
                    {
                        Url = "https://example.com/image.jpg"
                    }
                }
            }
        }
    }
};
```

### Processing Response Content

When working with responses, you'll need to handle the possibility that `Content` could be either a string or a JsonElement:

```csharp
// Example of handling response content
var response = await llmClient.CreateChatCompletionAsync(request);
var messageContent = response.Choices[0].Message.Content;

// Handle the content based on its type
string textContent;

if (messageContent is string stringContent)
{
    // Simple string content
    textContent = stringContent;
}
else if (messageContent is System.Text.Json.JsonElement jsonElement)
{
    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
    {
        // It's a string inside a JsonElement
        textContent = jsonElement.GetString();
    }
    else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
    {
        // It's an array of content parts - extract text from text parts
        var sb = new System.Text.StringBuilder();
        foreach (var element in jsonElement.EnumerateArray())
        {
            if (element.TryGetProperty("type", out var typeElement) &&
                typeElement.GetString() == "text" &&
                element.TryGetProperty("text", out var textElement))
            {
                sb.AppendLine(textElement.GetString());
            }
        }
        textContent = sb.ToString();
    }
    else
    {
        textContent = jsonElement.ToString();
    }
}
else
{
    // Fallback
    textContent = messageContent?.ToString() ?? string.Empty;
}
```

## Using Base64-Encoded Images

To include an image directly in your request (instead of linking to an external URL), you can use a data URL with a base64-encoded image:

```csharp
// Create an image content part with a base64-encoded image
public ImageUrlContentPart CreateImageContentPartFromFile(string imagePath)
{
    // Read the image file
    byte[] imageData = System.IO.File.ReadAllBytes(imagePath);
    
    // Convert to base64
    string base64 = Convert.ToBase64String(imageData);
    
    // Get the image MIME type based on file extension
    string mimeType = GetMimeTypeFromPath(imagePath);
    
    // Create the data URL
    string dataUrl = $"data:{mimeType};base64,{base64}";
    
    // Return the image content part
    return new ImageUrlContentPart
    {
        ImageUrl = new ImageUrl
        {
            Url = dataUrl,
            Detail = "auto"
        }
    };
}

private string GetMimeTypeFromPath(string imagePath)
{
    string extension = System.IO.Path.GetExtension(imagePath).ToLowerInvariant();
    
    return extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
```

## Supported Providers

Currently, the following providers support vision capabilities:

- OpenAI (via gpt-4-vision-preview and later models)

Other providers will extract text-only content from multimodal messages and operate in text-only mode.

## Token Counting

When using multimodal content, token counting works differently than with text-only content:

- Text content is counted based on the number of tokens in the text
- Image content tokens are calculated based on several factors:
  - Image resolution
  - Detail level requested
  - Image format

For example, in OpenAI's GPT-4 Vision implementation:
- Low-detail images use approximately 85 tokens
- High-detail images can use 4,000+ tokens for high-resolution images

The token counter in ConduitLLM provides an approximation of token usage when working with image content.

## Web UI Support

The ConduitLLM Web UI has been updated to support displaying both text and image content in chat messages. Images are displayed as responsive images within the chat interface.

## Examples

For a complete example of using vision capabilities, see the `VisionExample.cs` file in the `ConduitLLM.Examples` project.
