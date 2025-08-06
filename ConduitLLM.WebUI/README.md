# Conduit WebUI

Next.js-based web interface for the Conduit LLM Platform, built with React, TypeScript, and Mantine.

## Architecture Overview

The WebUI uses SDK React Query hooks directly for all API operations:

### Client-Side SDK Usage
- **Core SDK**: Used for LLM operations (chat, images, video, audio) with virtual key authentication
- **Admin SDK**: Used for admin operations (providers, keys, settings) with master key authentication

### Authentication Flow
1. Admin logs in through Clerk authentication
2. WebUI verifies user has `siteadmin: true` in Clerk metadata
3. Server uses `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` for backend API calls
4. All admin operations use master key authentication server-side

### Key Benefits
- 🚀 **Direct SDK Usage**: No proxy layer, reduced latency
- 🔄 **React Query Integration**: Built-in caching, optimistic updates
- 🔐 **Secure Authentication**: Virtual keys for client-side operations
- 📦 **Simplified Codebase**: Less code to maintain

## Features

- 🚀 **Next.js 15** with App Router and TypeScript
- 🎨 **Mantine UI** component library with custom theme
- 🔗 **Direct SDK Integration** with React Query hooks
- ⚡ **Real-time Updates** via SignalR
- 📊 **State Management** with Zustand and React Query
- 🎯 **Type Safety** throughout the application
- 🔐 **Automatic Virtual Key Management** for secure API access

## Quick Start

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Configure environment:**
   ```bash
   cp .env.example .env.local
   # Edit .env.local with your configuration
   ```

3. **Start development server:**
   ```bash
   npm run dev
   ```

4. **Open in browser:**
   ```
   http://localhost:3000
   ```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `NEXT_PUBLIC_CONDUIT_ADMIN_API_URL` | Admin API endpoint | `http://localhost:5002` |
| `NEXT_PUBLIC_CONDUIT_CORE_API_URL` | Core API endpoint | `http://localhost:5000` |
| `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` | Backend service authentication key | `alpha` |
| `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` | Clerk publishable key for authentication | Required |
| `CLERK_SECRET_KEY` | Clerk secret key for authentication | Required |
| `NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES` | Enable SignalR features | `true` |

## Project Structure

```
src/
├── app/                    # Next.js App Router pages
├── components/             # Reusable UI components
│   ├── ui/                # Base UI components
│   ├── forms/             # Form components
│   ├── charts/            # Data visualization
│   ├── layout/            # Layout components
│   └── realtime/          # SignalR components
├── hooks/                 # Custom React hooks
│   ├── signalr/           # SignalR-specific hooks
│   └── api/               # API integration hooks
├── lib/                   # Utilities and configurations
│   ├── clients/           # SDK client configurations
│   ├── auth/              # Authentication utilities
│   ├── signalr/           # SignalR connection management
│   └── utils/             # Helper functions
├── stores/                # Zustand stores
├── types/                 # TypeScript type definitions
└── styles/                # Mantine theme customization
```

## Available Scripts

- `npm run dev` - Start development server on port 3000
- `npm run build` - Build for production
- `npm run start` - Start production server
- `npm run lint` - Run ESLint
- `npm run type-check` - Run TypeScript type checking

## Development

### Adding New Pages

1. Create page component in `src/app/[page-name]/page.tsx`
2. Add navigation links in the layout components
3. Implement API integration using Conduit SDKs

### Using SDK React Query Hooks

The WebUI now uses SDK React Query hooks directly in components:

```typescript
// Using Core SDK hooks
import { useChatCompletion, useImageGeneration } from '@knn_labs/conduit-core-client/react-query';

function ChatComponent() {
  const { mutate: sendMessage } = useChatCompletion();
  
  const handleSend = (messages) => {
    sendMessage({ messages });
  };
}

// Using Admin SDK hooks
import { useProviders, useCreateProvider } from '@knn_labs/conduit-admin-client/react-query';

function ProvidersPage() {
  const { data: providers } = useProviders();
  const { mutate: createProvider } = useCreateProvider();
}
```

### Provider Setup

SDK providers are configured in the app layout:

```typescript
// lib/providers/ConduitProviders.tsx
import { ConduitProvider } from '@knn_labs/conduit-core-client/react-query';
import { ConduitAdminProvider } from '@knn_labs/conduit-admin-client/react-query';

export function ConduitProviders({ children }) {
  const { virtualKey } = useAuthStore();
  
  return (
    <ConduitProvider virtualKey={virtualKey} baseUrl={coreApiUrl}>
      <ConduitAdminProvider authKey={masterKey} baseUrl={adminApiUrl}>
        {children}
      </ConduitAdminProvider>
    </ConduitProvider>
  );
}
```

### Real-time Features

SignalR connections are managed centrally and provide real-time updates for:
- Virtual key spend tracking
- Provider health monitoring
- Task progress (image/video generation)
- Navigation state updates

## Video Generation

The WebUI provides a comprehensive video generation interface with real-time progress tracking through the SDK's unified interface.

### Features
- ✨ Real-time progress updates via SignalR
- 🔄 Automatic fallback to polling if connection fails
- 📊 Smooth progress bar animations
- 💬 Descriptive status messages
- 🎯 Queue management for multiple videos
- 🎨 Visual preview of generated videos

### Usage

```typescript
import { useVideoGeneration } from '@/app/videos/hooks/useVideoGeneration';

function VideoGenerator() {
  const { generateVideo, isGenerating, error } = useVideoGeneration();
  
  const handleGenerate = async () => {
    await generateVideo({
      prompt: "A serene lake at sunset",
      settings: { 
        model: "minimax-video-01",
        duration: 6,
        size: "1280x720",
        fps: 30
      }
    });
  };

  return (
    <div>
      <button onClick={handleGenerate} disabled={isGenerating}>
        Generate Video
      </button>
      {error && <div>Error: {error}</div>}
    </div>
  );
}
```

### Progress Tracking

The video generation hook automatically handles:
1. **SignalR Connection**: Establishes real-time connection for updates
2. **Progress Events**: Receives percentage, status, and messages
3. **Fallback Logic**: Switches to polling if SignalR fails
4. **State Management**: Updates UI with progress information

### Configuration

Enable/disable progress tracking features:

```typescript
// Use the enhanced video generation with progress tracking
const { generateVideo } = useVideoGeneration({
  useProgressTracking: true,  // Enable SDK progress tracking
  fallbackToPolling: true,    // Enable polling fallback
});
```

### Video Queue

The WebUI maintains a queue of video generation tasks:
- View all pending, running, and completed videos
- Cancel in-progress generations
- Download completed videos
- Retry failed generations

## Docker Deployment

The WebUI is configured to run as part of the ConduitLLM Docker stack:

```bash
# Build and start all services from the root directory
docker-compose up -d

# The WebUI will be available at http://localhost:3000
```

### Docker Environment Variables

The following environment variables are configured in docker-compose.yml:

- `NEXT_PUBLIC_CONDUIT_CORE_API_URL`: Public URL for Core API (browser access)
- `NEXT_PUBLIC_CONDUIT_ADMIN_API_URL`: Public URL for Admin API (browser access)
- `CONDUIT_API_BASE_URL`: Internal URL for Core API (server-side)
- `CONDUIT_ADMIN_API_BASE_URL`: Internal URL for Admin API (server-side)
- `CONDUIT_API_EXTERNAL_URL`: External URL for SignalR Core API connections
- `CONDUIT_ADMIN_API_EXTERNAL_URL`: External URL for SignalR Admin API connections
- `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`: Master key for Admin API authentication
- `SESSION_SECRET`: Secret key for session encryption
- `REDIS_URL`: Redis connection string for session storage

The application runs on port 3000 in the Docker environment.

## Contributing

1. Follow the existing code patterns and conventions
2. Use TypeScript for all new code
3. Add appropriate tests for new features
4. Update documentation as needed

## License

ISC