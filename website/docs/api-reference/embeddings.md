---
sidebar_position: 3
title: Embeddings API
description: Reference for Conduit's Embeddings API
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Embeddings API

The Embeddings API generates vector representations of text, which can be used for search, clustering, recommendations, and other natural language processing tasks.

## Endpoint

```
POST /v1/embeddings
```

## Request Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `model` | string | Yes | The ID of the embedding model to use |
| `input` | string/array | Yes | Text to embed (string or array of strings) |
| `user` | string | No | Unique identifier for the end-user |
| `encoding_format` | string | No | Output encoding format (default: 'float') |
| `dimensions` | integer | No | Specify embedding dimensions for models that support it |
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
curl http://localhost:5000/v1/embeddings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-embedding-model",
    "input": "The quick brown fox jumps over the lazy dog"
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

response = client.embeddings.create(
    model="my-embedding-model",
    input="The quick brown fox jumps over the lazy dog"
)

print(f"Embedding dimensions: {len(response.data[0].embedding)}")
print(f"First few values: {response.data[0].embedding[:5]}")
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
  const response = await openai.embeddings.create({
    model: 'my-embedding-model',
    input: 'The quick brown fox jumps over the lazy dog'
  });
  
  console.log(`Embedding dimensions: ${response.data[0].embedding.length}`);
  console.log(`First few values: ${response.data[0].embedding.slice(0, 5)}`);
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
            model = "my-embedding-model",
            input = "The quick brown fox jumps over the lazy dog"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("v1/embeddings", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine(responseBody);
    }
}
```

  </TabItem>
</Tabs>

## Multiple Inputs Example

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/embeddings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-embedding-model",
    "input": ["The quick brown fox", "jumps over the lazy dog"]
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

response = client.embeddings.create(
    model="my-embedding-model",
    input=["The quick brown fox", "jumps over the lazy dog"]
)

# Access each embedding
for i, data in enumerate(response.data):
    print(f"Embedding {i+1} dimensions: {len(data.embedding)}")
```

  </TabItem>
</Tabs>

## Response Format

```json
{
  "object": "list",
  "data": [
    {
      "object": "embedding",
      "embedding": [0.0023064255, -0.009327292, ...],
      "index": 0
    }
  ],
  "model": "my-embedding-model",
  "usage": {
    "prompt_tokens": 8,
    "total_tokens": 8
  }
}
```

## Specifying Dimensions

Some embedding models support different dimensions:

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/embeddings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "my-embedding-model",
    "input": "The quick brown fox jumps over the lazy dog",
    "dimensions": 256
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

response = client.embeddings.create(
    model="my-embedding-model",
    input="The quick brown fox jumps over the lazy dog",
    dimensions=256
)

print(f"Embedding dimensions: {len(response.data[0].embedding)}")
```

  </TabItem>
</Tabs>

## Conduit-Specific Extensions

### Cache Control

Control the caching behavior for this specific request:

```json
{
  "model": "my-embedding-model",
  "input": "The quick brown fox jumps over the lazy dog",
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
  "model": "my-embedding-model",
  "input": "The quick brown fox jumps over the lazy dog",
  "routing": {
    "strategy": "least_cost",
    "fallback_enabled": true,
    "provider_override": "cohere"
  }
}
```

## Best Practices

### Model Selection

Different embedding models have different characteristics:

- **Dimensions**: Higher dimensions can capture more information but use more storage
- **Semantic Richness**: Some models are better at capturing meaning
- **Multilingual Support**: Some models handle multiple languages better
- **Speed and Cost**: Smaller models are faster and cheaper

### Input Truncation

Most embedding models have a token limit. When exceeded:

- Inputs are automatically truncated
- A warning is included in the response
- Consider splitting long texts into smaller chunks

### Embeddings Storage

When storing embeddings:

- Use vector databases like Pinecone, Weaviate, or Milvus
- Or use specialized libraries like FAISS or HNSWLIB
- Store model ID with embeddings for compatibility

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

- [Chat Completions API](chat-completions): Generate conversational responses
- [Models API](models): List and filter available models
- [Virtual Keys](../features/virtual-keys): Learn about API key management