# Conduit WebUI

Next.js-based web interface for the Conduit LLM Platform, built with React, TypeScript, and Mantine.

## Features

- 🚀 **Next.js 15** with App Router and TypeScript
- 🎨 **Mantine UI** component library with custom theme
- 🔗 **Conduit SDK Integration** for Core and Admin APIs
- ⚡ **Real-time Updates** via SignalR
- 📊 **State Management** with Zustand and React Query
- 🎯 **Type Safety** throughout the application

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
   http://localhost:3001
   ```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `NEXT_PUBLIC_CONDUIT_ADMIN_API_URL` | Admin API endpoint | `http://localhost:5002` |
| `NEXT_PUBLIC_CONDUIT_CORE_API_URL` | Core API endpoint | `http://localhost:5000` |
| `CONDUIT_MASTER_KEY` | Master key for authentication | `alpha` |
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

- `npm run dev` - Start development server on port 3001
- `npm run build` - Build for production
- `npm run start` - Start production server
- `npm run lint` - Run ESLint
- `npm run type-check` - Run TypeScript type checking

## Development

### Adding New Pages

1. Create page component in `src/app/[page-name]/page.tsx`
2. Add navigation links in the layout components
3. Implement API integration using Conduit SDKs

### Using Conduit SDKs

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';

// Admin operations
const adminClient = ConduitAdminClient.fromEnvironment({
  masterKey: process.env.CONDUIT_MASTER_KEY
});

// Core operations
const coreClient = new ConduitCoreClient({
  baseUrl: process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL,
  virtualKey: userVirtualKey
});
```

### Real-time Features

SignalR connections are managed centrally and provide real-time updates for:
- Virtual key spend tracking
- Provider health monitoring
- Task progress (image/video generation)
- Navigation state updates

## Migration from Blazor WebUI

This project is a complete rewrite of the Blazor WebUI with enhanced features:
- Better real-time capabilities
- Modern React patterns
- Improved developer experience
- Enhanced performance
- Mobile-friendly responsive design

## Docker Deployment

The application is configured to run on port 3001 to work alongside the existing Blazor WebUI during migration.

## Contributing

1. Follow the existing code patterns and conventions
2. Use TypeScript for all new code
3. Add appropriate tests for new features
4. Update documentation as needed

## License

ISC