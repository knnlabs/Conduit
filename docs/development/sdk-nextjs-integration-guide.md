# Conduit SDK Next.js Integration Guide

A comprehensive guide for integrating the Conduit SDK into Next.js applications with TypeScript, covering both Pages Router and App Router patterns.

## Overview

This guide provides step-by-step instructions for integrating Conduit's Node.js SDK into Next.js applications, with practical examples for common use cases including chat interfaces, image generation, and admin dashboards.

## Documentation Structure

The Next.js integration guide has been organized into focused implementation guides:

### 🚀 Getting Started
- **[Next.js Setup & Configuration](./nextjs/setup-configuration.md)** - Initial setup and environment configuration
- **[Authentication Integration](./nextjs/authentication.md)** - NextAuth and session management
- **[SDK Client Setup](./nextjs/sdk-clients.md)** - Admin and Core client configuration

### 🎯 Core Features
- **[Chat Interface Implementation](./nextjs/chat-interface.md)** - Streaming chat with real-time updates
- **[Image Generation UI](./nextjs/image-generation.md)** - Image generation with gallery interface
- **[Video Generation Dashboard](./nextjs/video-generation.md)** - Async video generation with progress tracking

### 🛠️ Advanced Integration
- **[Admin Dashboard](./nextjs/admin-dashboard.md)** - Complete administrative interface
- **[Real-Time Features](./nextjs/realtime-features.md)** - SignalR integration and live updates
- **[Error Handling & Testing](./nextjs/error-handling.md)** - Production-ready error handling

## Quick Start

### Installation & Setup

```bash
# Create Next.js app with TypeScript
npx create-next-app@latest conduit-app --typescript --tailwind --eslint --app

# Install required dependencies
cd conduit-app
npm install @tanstack/react-query @mantine/core @mantine/hooks
npm install @conduit/admin-sdk @conduit/core-sdk  # When available
npm install next-auth

# Install development dependencies
npm install --save-dev @types/node
```

### Environment Configuration

```bash
# .env.local
NEXTAUTH_SECRET=your-nextauth-secret
NEXTAUTH_URL=http://localhost:3000

# Conduit API Configuration
CONDUIT_CORE_API_URL=http://localhost:5000
CONDUIT_ADMIN_API_URL=http://localhost:5002
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-master-key

# Optional: WebUI Virtual Key for core operations
CONDUIT_WEBUI_VIRTUAL_KEY=your-webui-virtual-key
```

### Basic App Structure

```
src/
├── app/                          # App Router (Next.js 13+)
│   ├── api/                      # API routes
│   │   ├── auth/                 # NextAuth configuration
│   │   ├── admin/                # Admin API endpoints
│   │   └── core/                 # Core API endpoints
│   ├── chat/                     # Chat interface pages
│   ├── images/                   # Image generation pages
│   └── admin/                    # Admin dashboard pages
├── components/                   # Reusable components
│   ├── chat/                     # Chat-specific components
│   ├── image/                    # Image generation components
│   └── ui/                       # Generic UI components
├── lib/                          # Utility libraries
│   ├── sdk/                      # SDK client configurations
│   ├── auth.ts                   # NextAuth configuration
│   └── utils.ts                  # Utility functions
└── types/                        # TypeScript type definitions
```

## Core Integration Examples

### Chat Interface with Streaming

```typescript
// components/chat/ChatInterface.tsx
'use client';

import { useState } from 'react';
import { Button, TextInput, Paper, Stack, Text } from '@mantine/core';
import { useMutation } from '@tanstack/react-query';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
}

export function ChatInterface() {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');

  const sendMessage = useMutation({
    mutationFn: async (content: string) => {
      const response = await fetch('/api/core/chat/completions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: 'gpt-4',
          messages: [...messages, { role: 'user', content }],
          stream: true,
        }),
      });

      if (!response.ok) throw new Error('Failed to send message');
      return response.body;
    },
    onSuccess: (stream) => {
      processStreamingResponse(stream);
    },
  });

  const processStreamingResponse = async (stream: ReadableStream | null) => {
    if (!stream) return;
    
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
                // Update messages with streaming content
                setMessages(prev => {
                  const newMessages = [...prev];
                  const lastMessage = newMessages[newMessages.length - 1];
                  
                  if (lastMessage?.role === 'assistant') {
                    newMessages[newMessages.length - 1] = {
                      ...lastMessage,
                      content: assistantMessage,
                    };
                  } else {
                    newMessages.push({
                      id: `assistant-${Date.now()}`,
                      role: 'assistant',
                      content: assistantMessage,
                    });
                  }
                  
                  return newMessages;
                });
              }
            } catch (e) {
              // Skip invalid JSON chunks
            }
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
  };

  const handleSend = () => {
    if (!input.trim()) return;
    
    // Add user message
    const userMessage: Message = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: input,
    };
    
    setMessages(prev => [...prev, userMessage]);
    sendMessage.mutate(input);
    setInput('');
  };

  return (
    <Stack>
      <Paper p="md" style={{ height: 400, overflowY: 'auto' }}>
        {messages.map((message) => (
          <div key={message.id} style={{ marginBottom: 16 }}>
            <Text size="sm" c="dimmed">{message.role}</Text>
            <Text>{message.content}</Text>
          </div>
        ))}
      </Paper>
      
      <div style={{ display: 'flex', gap: 8 }}>
        <TextInput
          flex={1}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Type your message..."
          onKeyPress={(e) => e.key === 'Enter' && handleSend()}
        />
        <Button 
          onClick={handleSend}
          loading={sendMessage.isPending}
          disabled={!input.trim()}
        >
          Send
        </Button>
      </div>
    </Stack>
  );
}
```

### API Route Setup

```typescript
// app/api/core/chat/completions/route.ts
import { NextRequest } from 'next/server';
import { getServerCoreClient } from '@/lib/sdk/server-core-client';

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const coreClient = getServerCoreClient();
    
    const response = await coreClient.chat.completions.create({
      model: body.model,
      messages: body.messages,
      stream: body.stream,
      temperature: body.temperature,
      max_tokens: body.max_tokens,
    });

    if (body.stream) {
      // Return streaming response
      return new Response(response.body, {
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          'Connection': 'keep-alive',
        },
      });
    }

    return Response.json(response);
  } catch (error) {
    console.error('Chat completion error:', error);
    return Response.json(
      { error: 'Failed to process chat completion' },
      { status: 500 }
    );
  }
}
```

### SDK Client Configuration

```typescript
// lib/sdk/server-core-client.ts
import { ConduitCoreClient } from '@conduit/core-sdk';

let coreClient: ConduitCoreClient | null = null;

export function getServerCoreClient(): ConduitCoreClient {
  if (!coreClient) {
    coreClient = new ConduitCoreClient({
      baseURL: process.env.CONDUIT_CORE_API_URL!,
      virtualKey: process.env.CONDUIT_WEBUI_VIRTUAL_KEY!,
    });
  }
  return coreClient;
}

// lib/sdk/server-admin-client.ts
import { ConduitAdminClient } from '@conduit/admin-sdk';

let adminClient: ConduitAdminClient | null = null;

export function getServerAdminClient(): ConduitAdminClient {
  if (!adminClient) {
    adminClient = new ConduitAdminClient({
      baseURL: process.env.CONDUIT_ADMIN_API_URL!,
      masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
    });
  }
  return adminClient;
}
```

### Image Generation Component

```typescript
// components/image/ImageGenerator.tsx
'use client';

import { useState } from 'react';
import { Button, TextInput, Grid, Card, Image, Text } from '@mantine/core';
import { useMutation } from '@tanstack/react-query';

interface GeneratedImage {
  id: string;
  url: string;
  prompt: string;
  timestamp: Date;
}

export function ImageGenerator() {
  const [prompt, setPrompt] = useState('');
  const [images, setImages] = useState<GeneratedImage[]>([]);

  const generateImage = useMutation({
    mutationFn: async (prompt: string) => {
      const response = await fetch('/api/core/images/generations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          prompt,
          model: 'dall-e-3',
          size: '1024x1024',
          quality: 'hd',
        }),
      });

      if (!response.ok) throw new Error('Image generation failed');
      return response.json();
    },
    onSuccess: (data) => {
      const newImage: GeneratedImage = {
        id: Date.now().toString(),
        url: data.data.data[0].url,
        prompt,
        timestamp: new Date(),
      };
      setImages(prev => [newImage, ...prev]);
      setPrompt('');
    },
  });

  return (
    <div>
      <Card mb="xl">
        <TextInput
          placeholder="Describe the image you want to generate..."
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          onKeyPress={(e) => e.key === 'Enter' && generateImage.mutate(prompt)}
        />
        <Button
          mt="sm"
          onClick={() => generateImage.mutate(prompt)}
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
                <Image src={image.url} alt={image.prompt} height={200} />
              </Card.Section>
              <Text size="sm" mt="sm" lineClamp={2}>
                {image.prompt}
              </Text>
              <Text size="xs" c="dimmed" mt="xs">
                {image.timestamp.toLocaleString()}
              </Text>
            </Card>
          </Grid.Col>
        ))}
      </Grid>
    </div>
  );
}
```

## App Router vs Pages Router

### App Router (Recommended)
```typescript
// app/chat/page.tsx
import { ChatInterface } from '@/components/chat/ChatInterface';

export default function ChatPage() {
  return (
    <div>
      <h1>Chat with AI</h1>
      <ChatInterface />
    </div>
  );
}
```

### Pages Router (Legacy)
```typescript
// pages/chat.tsx
import { ChatInterface } from '@/components/chat/ChatInterface';
import { GetServerSideProps } from 'next';

export default function ChatPage() {
  return (
    <div>
      <h1>Chat with AI</h1>
      <ChatInterface />
    </div>
  );
}

export const getServerSideProps: GetServerSideProps = async () => {
  return { props: {} };
};
```

## Authentication Integration

### NextAuth Configuration

```typescript
// app/api/auth/[...nextauth]/route.ts
import NextAuth from 'next-auth';
import { authOptions } from '@/lib/auth';

const handler = NextAuth(authOptions);
export { handler as GET, handler as POST };

// lib/auth.ts
import { NextAuthOptions } from 'next-auth';
import CredentialsProvider from 'next-auth/providers/credentials';

export const authOptions: NextAuthOptions = {
  providers: [
    CredentialsProvider({
      name: 'credentials',
      credentials: {
        username: { label: 'Username', type: 'text' },
        password: { label: 'Password', type: 'password' },
      },
      async authorize(credentials) {
        // Implement your authentication logic
        if (credentials?.username === 'admin' && credentials?.password === 'admin') {
          return { id: '1', name: 'Admin', email: 'admin@example.com' };
        }
        return null;
      },
    }),
  ],
  session: { strategy: 'jwt' },
  pages: {
    signIn: '/auth/signin',
  },
};
```

## State Management with React Query

```typescript
// lib/query-client.ts
import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      refetchOnWindowFocus: false,
    },
  },
});

// app/layout.tsx
'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import { MantineProvider } from '@mantine/core';
import { queryClient } from '@/lib/query-client';

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <QueryClientProvider client={queryClient}>
          <MantineProvider>
            {children}
          </MantineProvider>
        </QueryClientProvider>
      </body>
    </html>
  );
}
```

## Error Handling

```typescript
// lib/error-handling.ts
export class ConduitAPIError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public details?: any
  ) {
    super(message);
    this.name = 'ConduitAPIError';
  }
}

export function handleAPIError(error: any): never {
  if (error.response) {
    throw new ConduitAPIError(
      error.response.data?.error?.message || 'API Error',
      error.response.status,
      error.response.data
    );
  }
  
  throw new ConduitAPIError(error.message || 'Unknown error', 500);
}

// components/ErrorBoundary.tsx
'use client';

import { Component, ReactNode } from 'react';
import { Alert } from '@mantine/core';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback || (
        <Alert color="red" title="Something went wrong">
          {this.state.error?.message || 'An unexpected error occurred'}
        </Alert>
      );
    }

    return this.props.children;
  }
}
```

## Testing

```typescript
// __tests__/components/ChatInterface.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ChatInterface } from '@/components/chat/ChatInterface';

// Mock fetch
global.fetch = jest.fn();

const createTestQueryClient = () => new QueryClient({
  defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
});

describe('ChatInterface', () => {
  beforeEach(() => {
    (fetch as jest.Mock).mockClear();
  });

  it('sends a message when form is submitted', async () => {
    const queryClient = createTestQueryClient();
    
    (fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      body: new ReadableStream(),
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ChatInterface />
      </QueryClientProvider>
    );

    const input = screen.getByPlaceholderText('Type your message...');
    const button = screen.getByText('Send');

    fireEvent.change(input, { target: { value: 'Hello' } });
    fireEvent.click(button);

    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith('/api/core/chat/completions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: 'gpt-4',
          messages: [{ role: 'user', content: 'Hello' }],
          stream: true,
        }),
      });
    });
  });
});
```

## Deployment

### Production Environment Variables

```bash
# .env.production
NEXTAUTH_SECRET=production-secret
NEXTAUTH_URL=https://yourdomain.com

CONDUIT_CORE_API_URL=https://core-api.yourdomain.com
CONDUIT_ADMIN_API_URL=https://admin-api.yourdomain.com
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=production-master-key
CONDUIT_WEBUI_VIRTUAL_KEY=production-webui-key
```

### Docker Configuration

```dockerfile
# Dockerfile
FROM node:18-alpine AS dependencies
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --only=production

FROM node:18-alpine AS build
WORKDIR /app
COPY . .
COPY --from=dependencies /app/node_modules ./node_modules
RUN npm run build

FROM node:18-alpine AS runtime
WORKDIR /app
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/package.json ./package.json
COPY --from=dependencies /app/node_modules ./node_modules

EXPOSE 3000
CMD ["npm", "start"]
```

## Best Practices

### Performance
- Use React Query for efficient data fetching and caching
- Implement proper error boundaries for robust error handling
- Use Next.js Image component for optimized image loading
- Implement proper SEO with metadata API

### Security
- Never expose master keys in client-side code
- Use environment variables for all sensitive configuration
- Implement proper CORS and CSP headers
- Validate all user inputs on both client and server

### User Experience
- Provide loading states for all async operations
- Implement optimistic updates where appropriate
- Use proper error messaging and recovery options
- Ensure accessibility compliance with proper ARIA labels

## Related Documentation

- [Admin API Examples](./admin-api/examples.md) - Administrative API usage examples
- [Real-Time API Guide](./real-time-api-guide.md) - Real-time features and SignalR integration
- [Integration Examples](./examples/INTEGRATION-EXAMPLES.md) - Broader integration patterns