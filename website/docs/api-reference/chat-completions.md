---
sidebar_position: 2
title: Chat Completions API
description: Reference for Conduit's Chat Completions API
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Chat Completions API

The Chat Completions API is the primary interface for conversational interactions with language models through Conduit. It follows the OpenAI chat completions format.

## Endpoint

```
POST /v1/chat/completions
```

## Request Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `model` | string | Yes | The ID of the model to use (your configured virtual model name) |
| `messages` | array | Yes | Array of message objects with `role` and `content` |
| `temperature` | number | No | Controls randomness (0-2, default 1) |
| `top_p` | number | No | Controls diversity via nucleus sampling (0-1, default 1) |
| `n` | integer | No | Number of completions to generate (default 1) |
| `stream` | boolean | No | Stream partial results as they're generated (default false) |
| `stop` | string/array | No | Sequence(s) where the API will stop generating |
| `max_tokens` | integer | No | Maximum number of tokens to generate |
| `presence_penalty` | number | No | Penalty for new tokens based on presence in context (-2 to 2) |
| `frequency_penalty` | number | No | Penalty for new tokens based on frequency in context (-2 to 2) |
| `logit_bias` | object | No | Modify likelihood of specified tokens |
| `user` | string | No | Unique identifier for the end-user |
| `tools` | array | No | List of tools the model may call |
| `tool_choice` | string/object | No | Controls how the model calls tools |
| `response_format` | object | No | Format of the response (eg. JSON mode) |
| `seed` | integer | No | Seed for deterministic outputs |
| `cache_control` | object | No | Control caching behavior |
| `routing` | object | No | Custom routing options for this request |

## Basic Example

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
    {label: 'JavaScript', value: 'javascript'},
    {label: 'C#', value: 'csharp'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-gpt4",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ]
  }'
```

  </TabItem>
  <TabItem value="python">

```python
from openai import OpenAI

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="http://localhost:5000/v1"
)

response = client.chat.completions.create(
    model="my-gpt4",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "Hello!"}
    ]
)

print(response.choices[0].message.content)
```

  </TabItem>
  <TabItem value="javascript">

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'http://localhost:5000/v1'
});

async function main() {
  const completion = await openai.chat.completions.create({
    model: 'my-gpt4',
    messages: [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ],
  });
  
  console.log(completion.choices[0].message.content);
}

main();
```

  </TabItem>
  <TabItem value="csharp">

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://localhost:5000/");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer condt_your_virtual_key");

        var request = new
        {
            model = "my-gpt4",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = "Hello!" }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("v1/chat/completions", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine(responseBody);
    }
}
```

  </TabItem>
</Tabs>

## Response Format

```json
{
  "id": "chatcmpl-abc123",
  "object": "chat.completion",
  "created": 1677858242,
  "model": "my-gpt4",
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "Hello! How can I help you today?"
      },
      "finish_reason": "stop",
      "index": 0
    }
  ],
  "usage": {
    "prompt_tokens": 18,
    "completion_tokens": 8,
    "total_tokens": 26
  }
}
```

## Streaming Example

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
    {label: 'JavaScript', value: 'javascript'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-gpt4",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ],
    "stream": true
  }'
```

  </TabItem>
  <TabItem value="python">

```python
from openai import OpenAI

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="http://localhost:5000/v1"
)

stream = client.chat.completions.create(
    model="my-gpt4",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "Hello!"}
    ],
    stream=True
)

for chunk in stream:
    if chunk.choices[0].delta.content is not None:
        print(chunk.choices[0].delta.content, end="")
```

  </TabItem>
  <TabItem value="javascript">

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'http://localhost:5000/v1'
});

async function main() {
  const stream = await openai.chat.completions.create({
    model: 'my-gpt4',
    messages: [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ],
    stream: true,
  });
  
  for await (const chunk of stream) {
    process.stdout.write(chunk.choices[0]?.delta?.content || '');
  }
}

main();
```

  </TabItem>
</Tabs>

## Multimodal Example

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-vision-model",
    "messages": [
      {
        "role": "user",
        "content": [
          {
            "type": "text",
            "text": "What is in this image?"
          },
          {
            "type": "image_url",
            "image_url": {
              "url": "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAA..."
            }
          }
        ]
      }
    ]
  }'
```

  </TabItem>
  <TabItem value="python">

```python
from openai import OpenAI
import base64

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="http://localhost:5000/v1"
)

# Function to encode the image
def encode_image(image_path):
    with open(image_path, "rb") as image_file:
        return base64.b64encode(image_file.read()).decode('utf-8')

# Path to your image
image_path = "path/to/your/image.jpg"

# Getting the base64 string
base64_image = encode_image(image_path)

response = client.chat.completions.create(
    model="my-vision-model",
    messages=[
        {
            "role": "user",
            "content": [
                {
                    "type": "text",
                    "text": "What is in this image?"
                },
                {
                    "type": "image_url",
                    "image_url": {
                        "url": f"data:image/jpeg;base64,{base64_image}"
                    }
                }
            ]
        }
    ]
)

print(response.choices[0].message.content)
```

  </TabItem>
</Tabs>

## Tool Calling Example

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-gpt4",
    "messages": [
      {"role": "user", "content": "What is the weather in San Francisco?"}
    ],
    "tools": [
      {
        "type": "function",
        "function": {
          "name": "get_weather",
          "description": "Get the current weather in a given location",
          "parameters": {
            "type": "object",
            "properties": {
              "location": {
                "type": "string",
                "description": "The city and state, e.g. San Francisco, CA"
              },
              "unit": {
                "type": "string",
                "enum": ["celsius", "fahrenheit"]
              }
            },
            "required": ["location"]
          }
        }
      }
    ]
  }'
```

  </TabItem>
  <TabItem value="python">

```python
from openai import OpenAI

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="http://localhost:5000/v1"
)

response = client.chat.completions.create(
    model="my-gpt4",
    messages=[
        {"role": "user", "content": "What is the weather in San Francisco?"}
    ],
    tools=[
        {
            "type": "function",
            "function": {
                "name": "get_weather",
                "description": "Get the current weather in a given location",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "location": {
                            "type": "string",
                            "description": "The city and state, e.g. San Francisco, CA"
                        },
                        "unit": {
                            "type": "string",
                            "enum": ["celsius", "fahrenheit"]
                        }
                    },
                    "required": ["location"]
                }
            }
        }
    ]
)

print(response.choices[0].message)
```

  </TabItem>
</Tabs>

## Conduit-Specific Extensions

### Cache Control

Control the caching behavior for this specific request:

```json
{
  "model": "my-gpt4",
  "messages": [...],
  "cache_control": {
    "no_cache": false,
    "ttl": 7200
  }
}
```

### Custom Routing

Override the default routing strategy for this specific request:

```json
{
  "model": "my-gpt4",
  "messages": [...],
  "routing": {
    "strategy": "least_cost",
    "fallback_enabled": true,
    "provider_override": "anthropic"
  }
}
```

## Error Codes

| HTTP Code | Error Type | Description |
|-----------|------------|-------------|
| 400 | invalid_request_error | The request was malformed |
| 401 | authentication_error | Invalid or missing API key |
| 403 | permission_error | The API key doesn't have permission |
| 404 | not_found_error | The requested model was not found |
| 429 | rate_limit_error | Rate limit exceeded |
| 500 | server_error | Server error |

## Next Steps

- [Embeddings API](embeddings): Generate vector embeddings
- [Models API](models): List and filter available models
- [Virtual Keys](../features/virtual-keys): Learn about API key management