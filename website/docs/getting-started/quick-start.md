---
sidebar_position: 2
title: Quick Start
description: Get started with Conduit in under 5 minutes
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Quick Start Guide

This guide will help you make your first request to Conduit in just a few minutes. It assumes you've already [installed Conduit](installation).

## Step 1: Access the Web UI

Open your browser and navigate to `http://localhost:5001` (or the port you configured).

## Step 2: Log In with the Master Key

Enter the master key you configured during installation to access the admin dashboard.

## Step 3: Add Provider Credentials

1. Navigate to **Configuration > Provider Credentials**
2. Click **Add Provider Credential**
3. Select a provider (e.g., OpenAI)
4. Enter your API key and other required credentials
5. Click **Save**

:::tip
You only need to add credentials for the providers you intend to use.
:::

## Step 4: Create a Virtual Key

1. Navigate to **Virtual Keys**
2. Click **Create New Key**
3. Provide a name and description
4. Set permissions and rate limits
5. Click **Create**
6. Copy the generated key (it starts with `condt_`)

## Step 5: Make Your First Request

Now you can make requests to Conduit using your virtual key. The API is OpenAI-compatible, so you can use existing OpenAI client libraries.

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
    "model": "gpt-3.5-turbo",
    "messages": [{"role": "user", "content": "Hello from Conduit!"}]
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
    model="gpt-3.5-turbo",
    messages=[{"role": "user", "content": "Hello from Conduit!"}]
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
    messages: [{ role: 'user', content: 'Hello from Conduit!' }],
    model: 'gpt-3.5-turbo',
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
        // Configure HttpClient
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://localhost:5000/");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer condt_your_virtual_key");

        // Prepare request
        var request = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "user", content = "Hello from Conduit!" }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Send request
        var response = await client.PostAsync("v1/chat/completions", content);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine(jsonResponse);
    }
}
```

  </TabItem>
</Tabs>

## Next Steps

Now that you've made your first request, you can:

- Configure [Model Routing](../features/model-routing) to use specific providers
- Set up [Caching](../guides/cache-configuration) to improve performance and reduce costs
- Create additional [Virtual Keys](../features/virtual-keys) with different permissions
- Explore the [API Reference](../api-reference/overview) for all available endpoints

## Troubleshooting

If you encounter issues:

- Ensure your virtual key has the necessary permissions
- Check that you've added credentials for the provider you're trying to use
- Verify that the model you're requesting is supported by your configuration
- Check the server logs for more detailed error information