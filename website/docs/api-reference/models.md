---
sidebar_position: 4
title: Models API
description: Reference for Conduit's Models API
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Models API

The Models API allows you to list and retrieve information about the models available through Conduit.

## List Models

Lists the models available for use through Conduit.

### Endpoint

```
GET /v1/models
```

### Headers

| Header | Value | Required | Description |
|--------|-------|----------|-------------|
| Authorization | Bearer condt_your_virtual_key | Yes | Your Conduit virtual key |

### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `capability` | string | No | Filter by model capability (e.g., 'chat', 'embeddings') |
| `provider` | string | No | Filter by provider (e.g., 'openai', 'anthropic') |

### Example

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
curl http://localhost:5000/v1/models \
  -H "Authorization: Bearer condt_your_virtual_key"
```

  </TabItem>
  <TabItem value="python">

```python
from openai import OpenAI

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="http://localhost:5000/v1"
)

models = client.models.list()
for model in models.data:
    print(model.id)
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
  const models = await openai.models.list();
  models.data.forEach(model => {
    console.log(model.id);
  });
}

main();
```

  </TabItem>
  <TabItem value="csharp">

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://localhost:5000/");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer condt_your_virtual_key");

        var response = await client.GetAsync("v1/models");
        var responseBody = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine(responseBody);
    }
}
```

  </TabItem>
</Tabs>

### Response Format

```json
{
  "object": "list",
  "data": [
    {
      "id": "my-gpt4",
      "object": "model",
      "created": 1677610602,
      "owned_by": "conduit",
      "provider": "anthropic",
      "provider_model": "claude-3-opus-20240229",
      "capabilities": ["chat", "function_calling", "vision"]
    },
    {
      "id": "my-embedding-model",
      "object": "model",
      "created": 1677649963,
      "owned_by": "conduit",
      "provider": "openai",
      "provider_model": "text-embedding-ada-002",
      "capabilities": ["embeddings"]
    }
  ]
}
```

## Retrieve Model

Retrieves a specific model's information.

### Endpoint

```
GET /v1/models/{model_id}
```

### Example

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
    {label: 'Python', value: 'python'},
    {label: 'JavaScript', value: 'javascript'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/models/my-gpt4 \
  -H "Authorization: Bearer condt_your_virtual_key"
```

  </TabItem>
  <TabItem value="python">

```python
from openai import OpenAI

client = OpenAI(
    api_key="condt_your_virtual_key",
    base_url="http://localhost:5000/v1"
)

model = client.models.retrieve("my-gpt4")
print(model)
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
  const model = await openai.models.retrieve("my-gpt4");
  console.log(model);
}

main();
```

  </TabItem>
</Tabs>

### Response Format

```json
{
  "id": "my-gpt4",
  "object": "model",
  "created": 1677610602,
  "owned_by": "conduit",
  "provider": "anthropic",
  "provider_model": "claude-3-opus-20240229",
  "capabilities": ["chat", "function_calling", "vision"],
  "context_length": 200000,
  "status": "available"
}
```

## Conduit Extensions

Conduit includes additional model-related endpoints:

### List Provider Models

Lists all available models from a specific provider.

```
GET /v1/provider-models/{provider}
```

<Tabs
  defaultValue="curl"
  values={[
    {label: 'cURL', value: 'curl'},
  ]}>
  <TabItem value="curl">

```bash
curl http://localhost:5000/v1/provider-models/openai \
  -H "Authorization: Bearer condt_your_virtual_key"
```

  </TabItem>
</Tabs>

### Model Capabilities

The `capabilities` field in model responses indicates what features a model supports:

| Capability | Description |
|------------|-------------|
| `chat` | Supports chat completions |
| `completions` | Supports text completions |
| `embeddings` | Supports text embeddings |
| `function_calling` | Supports function calling / tools |
| `vision` | Supports image inputs |
| `streaming` | Supports streaming responses |
| `json_mode` | Supports JSON mode output |

### Model Status

The `status` field can have these values:

| Status | Description |
|--------|-------------|
| `available` | Model is available for use |
| `unavailable` | Model is temporarily unavailable |
| `deprecated` | Model is deprecated and may be removed |
| `limited` | Model has usage limitations |

## Best Practices

### Virtual Model Abstraction

Conduit virtual models abstract away specific provider models. This means:

- Your applications should use the virtual model names (`my-gpt4`)
- The underlying provider model can be changed without affecting your code
- Different provider models can be grouped under the same virtual model name

### Model Discovery Workflow

1. List all available models with the Models API
2. Filter models by capability for your use case
3. Test models to find the best balance of performance vs. cost
4. Configure model mappings in Conduit for flexibility

### Error Handling

| HTTP Code | Error Type | Description |
|-----------|------------|-------------|
| 401 | authentication_error | Invalid or missing API key |
| 403 | permission_error | The API key doesn't have permission |
| 404 | not_found_error | The requested model was not found |
| 429 | rate_limit_error | Rate limit exceeded |
| 500 | server_error | Server error |

## Next Steps

- [Chat Completions API](chat-completions): Generate conversational responses
- [Embeddings API](embeddings): Generate vector embeddings
- [Model Routing](../features/model-routing): Learn about advanced routing options