# Conduit SDK Integration Examples

This guide provides practical examples of integrating the Conduit SDK into various application scenarios. Each example includes complete, working code that can be adapted to your specific needs.

## Documentation Structure

The integration examples have been organized by application type and use case:

### 🎯 Core Applications
- **[Chat Application](./integrations/chat-application.md)** - Complete chat interface with streaming
- **[Image Generation Gallery](./integrations/image-gallery.md)** - Image generation with gallery UI
- **[Video Processing Pipeline](./integrations/video-pipeline.md)** - Async video generation and processing

### 🛠️ Administrative Tools
- **[Admin Dashboard](./integrations/admin-dashboard.md)** - Full administrative interface
- **[Virtual Key Management](./integrations/key-management.md)** - Key creation and monitoring
- **[Real-time Monitoring](./integrations/realtime-monitoring.md)** - Live metrics and status tracking

### 🔧 Advanced Integrations
- **[Batch Processing](./integrations/batch-processing.md)** - Large-scale operations and queuing
- **[Webhook Integration](./integrations/webhooks.md)** - Event-driven architecture patterns
- **[Cache Statistics](./integrations/cache-stats.md)** - Performance monitoring and optimization

## Quick Start Examples

### Chat Interface with Streaming

```typescript
// components/ChatInterface.tsx
'use client';

import { useState, useRef, useEffect } from 'react';
import { Card, TextInput, Button, ScrollArea, Group, Text } from '@mantine/core';
import { IconSend } from '@tabler/icons-react';
import { useMutation } from '@tanstack/react-query';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

export function ChatInterface({ virtualKey }: { virtualKey: string }) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const scrollAreaRef = useRef<HTMLDivElement>(null);
  
  const sendMessage = useMutation({
    mutationFn: async (content: string) => {
      const response = await fetch('/api/core/chat/completions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          virtual_key: virtualKey,
          model: 'gpt-4',
          messages: [...messages, { role: 'user', content }],
          stream: true,
        }),
      });

      if (!response.ok) throw new Error('Failed to send message');
      return response.body;
    },
    onSuccess: (stream) => {
      processStream(stream);
    },
  });

  const processStream = async (stream: ReadableStream) => {
    const reader = stream.getReader();
    const decoder = new TextDecoder();
    let assistantMessage = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value);
        const lines = chunk.split('\n');

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6);
            if (data === '[DONE]') return;

            try {
              const parsed = JSON.parse(data);
              const content = parsed.choices?.[0]?.delta?.content;
              if (content) {
                assistantMessage += content;
                // Update UI with streaming content
                setMessages(prev => [
                  ...prev.slice(0, -1),
                  {
                    id: 'assistant-' + Date.now(),
                    role: 'assistant',
                    content: assistantMessage,
                    timestamp: new Date(),
                  },
                ]);
              }
            } catch (e) {
              // Skip invalid JSON
            }
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
  };

  return (
    <Card>
      <ScrollArea h={400} ref={scrollAreaRef}>
        {messages.map((message) => (
          <div key={message.id} className={`message message--${message.role}`}>
            <Text size="sm" c="dimmed">{message.role}</Text>
            <Text>{message.content}</Text>
          </div>
        ))}
      </ScrollArea>
      
      <Group mt="md">
        <TextInput
          flex={1}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Type your message..."
          onKeyPress={(e) => {
            if (e.key === 'Enter') {
              sendMessage.mutate(input);
              setInput('');
            }
          }}
        />
        <Button 
          onClick={() => {
            sendMessage.mutate(input);
            setInput('');
          }}
          loading={sendMessage.isPending}
        >
          <IconSend size={16} />
        </Button>
      </Group>
    </Card>
  );
}
```

### Image Generation Gallery

```typescript
// components/ImageGallery.tsx
'use client';

import { useState } from 'react';
import { Button, TextInput, Grid, Card, Image, Text, Progress } from '@mantine/core';
import { useMutation, useQuery } from '@tanstack/react-query';

interface GeneratedImage {
  id: string;
  prompt: string;
  url: string;
  createdAt: Date;
  status: 'generating' | 'completed' | 'failed';
}

export function ImageGallery({ virtualKey }: { virtualKey: string }) {
  const [prompt, setPrompt] = useState('');
  const [images, setImages] = useState<GeneratedImage[]>([]);

  const generateImage = useMutation({
    mutationFn: async (prompt: string) => {
      const response = await fetch('/api/core/images/generations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          virtual_key: virtualKey,
          prompt,
          model: 'dall-e-3',
          size: '1024x1024',
          quality: 'hd',
        }),
      });

      if (!response.ok) throw new Error('Generation failed');
      return response.json();
    },
    onSuccess: (data) => {
      const newImage: GeneratedImage = {
        id: data.data.created.toString(),
        prompt,
        url: data.data.data[0].url,
        createdAt: new Date(),
        status: 'completed',
      };
      setImages(prev => [newImage, ...prev]);
    },
  });

  return (
    <div>
      <Card mb="lg">
        <TextInput
          placeholder="Describe the image you want to generate..."
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          onKeyPress={(e) => {
            if (e.key === 'Enter') {
              generateImage.mutate(prompt);
              setPrompt('');
            }
          }}
        />
        <Button
          mt="sm"
          onClick={() => {
            generateImage.mutate(prompt);
            setPrompt('');
          }}
          loading={generateImage.isPending}
          disabled={!prompt.trim()}
        >
          Generate Image
        </Button>
      </Card>

      <Grid>
        {images.map((image) => (
          <Grid.Col key={image.id} span={{ base: 12, md: 6, lg: 4 }}>
            <Card>
              <Card.Section>
                <Image
                  src={image.url}
                  alt={image.prompt}
                  height={200}
                  fallbackSrc="/placeholder.png"
                />
              </Card.Section>
              <Text size="sm" mt="sm" lineClamp={2}>
                {image.prompt}
              </Text>
              <Text size="xs" c="dimmed" mt="xs">
                {image.createdAt.toLocaleString()}
              </Text>
            </Card>
          </Grid.Col>
        ))}
      </Grid>
    </div>
  );
}
```

### Admin Dashboard Integration

```typescript
// components/AdminDashboard.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { Grid, Card, Text, Badge, Progress, Group } from '@mantine/core';

export function AdminDashboard() {
  const { data: virtualKeys } = useQuery({
    queryKey: ['virtual-keys'],
    queryFn: async () => {
      const response = await fetch('/api/admin/virtual-keys');
      if (!response.ok) throw new Error('Failed to fetch keys');
      return response.json();
    },
  });

  const { data: providers } = useQuery({
    queryKey: ['providers'],
    queryFn: async () => {
      const response = await fetch('/api/admin/providers');
      if (!response.ok) throw new Error('Failed to fetch providers');
      return response.json();
    },
  });

  const { data: metrics } = useQuery({
    queryKey: ['metrics'],
    queryFn: async () => {
      const response = await fetch('/api/admin/metrics');
      if (!response.ok) throw new Error('Failed to fetch metrics');
      return response.json();
    },
    refetchInterval: 30000, // Refresh every 30 seconds
  });

  return (
    <Grid>
      <Grid.Col span={{ base: 12, md: 4 }}>
        <Card>
          <Text size="lg" fw={500}>Virtual Keys</Text>
          <Text size="xl" mt="xs">{virtualKeys?.data?.length || 0}</Text>
          <Text size="sm" c="dimmed">Active API keys</Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, md: 4 }}>
        <Card>
          <Text size="lg" fw={500}>Providers</Text>
          <Group mt="xs">
            {providers?.data?.map((provider: any) => (
              <Badge
                key={provider.id}
                color={provider.health.status === 'healthy' ? 'green' : 'red'}
                variant="light"
              >
                {provider.name}
              </Badge>
            ))}
          </Group>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, md: 4 }}>
        <Card>
          <Text size="lg" fw={500}>Usage Today</Text>
          <Text size="xl" mt="xs">{metrics?.data?.requestsToday || 0}</Text>
          <Progress
            value={(metrics?.data?.requestsToday / metrics?.data?.dailyLimit) * 100}
            mt="xs"
          />
        </Card>
      </Grid.Col>
    </Grid>
  );
}
```

## Integration Patterns

### Authentication Handling
```typescript
// utils/auth.ts
export class ConduitAuth {
  constructor(private virtualKey: string) {}

  getHeaders() {
    return {
      'Content-Type': 'application/json',
      'X-Virtual-Key': this.virtualKey,
    };
  }

  async request(endpoint: string, options: RequestInit = {}) {
    const response = await fetch(endpoint, {
      ...options,
      headers: {
        ...this.getHeaders(),
        ...options.headers,
      },
    });

    if (!response.ok) {
      throw new Error(`Request failed: ${response.statusText}`);
    }

    return response.json();
  }
}
```

### Error Handling
```typescript
// utils/error-handling.ts
export class ConduitError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public details?: any
  ) {
    super(message);
    this.name = 'ConduitError';
  }
}

export function handleSDKError(error: any): never {
  if (error.response) {
    throw new ConduitError(
      error.response.data?.error?.message || 'API Error',
      error.response.status,
      error.response.data
    );
  }
  
  if (error.message) {
    throw new ConduitError(error.message, 500);
  }
  
  throw new ConduitError('Unknown error occurred', 500);
}
```

### React Query Integration
```typescript
// hooks/useConduit.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ConduitAuth } from '../utils/auth';

export function useConduit(virtualKey: string) {
  const auth = new ConduitAuth(virtualKey);
  const queryClient = useQueryClient();

  const chatCompletion = useMutation({
    mutationFn: (params: ChatParams) => 
      auth.request('/api/core/chat/completions', {
        method: 'POST',
        body: JSON.stringify(params),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['chat-history'] });
    },
  });

  const imageGeneration = useMutation({
    mutationFn: (params: ImageParams) =>
      auth.request('/api/core/images/generations', {
        method: 'POST',
        body: JSON.stringify(params),
      }),
  });

  return { chatCompletion, imageGeneration };
}
```

## Framework-Specific Patterns

### Next.js Integration
- API routes for server-side SDK calls
- Client components for interactive features
- Server components for data fetching
- Middleware for authentication

### React Integration
- Custom hooks for SDK operations
- Context providers for global state
- Error boundaries for robust error handling
- Suspense for loading states

### Node.js Backend
- Express middleware for authentication
- Service classes for business logic
- Queue systems for async operations
- Webhook handlers for events

## Best Practices

### Performance
- Use React Query for efficient data fetching
- Implement proper caching strategies
- Optimize for mobile performance
- Use streaming for real-time features

### Error Handling
- Implement comprehensive error boundaries
- Provide meaningful error messages
- Log errors for debugging
- Implement retry mechanisms

### Security
- Never expose virtual keys in client code
- Use environment variables for sensitive data
- Implement proper CORS policies
- Validate all user inputs

### User Experience
- Provide loading states for all operations
- Implement optimistic updates where appropriate
- Use proper error messaging
- Ensure accessibility compliance

## Related Documentation

- [SDK Integration Guide](../sdk-nextjs-integration-guide.md) - Next.js specific integration patterns
- [Real-Time API Guide](../real-time-api-guide.md) - Real-time features and WebSocket integration
- [Admin API Examples](../admin-api/examples.md) - Administrative API usage examples