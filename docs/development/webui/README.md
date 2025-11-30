# WebAdmin Development Documentation

This directory contains development documentation specific to the Conduit WebAdmin (Next.js application).

## 📚 Available Documentation

### [Troubleshooting Guide](troubleshooting.md)
Comprehensive troubleshooting guide for WebAdmin development issues.

**Topics Covered**:
- Connection issues (ECONNREFUSED, CORS errors)
- Authentication problems (session persistence, master key)
- SDK client errors (initialization, type errors)
- SignalR/Real-time issues (WebSocket connection, progress updates)
- Performance problems (slow responses, memory leaks)
- API response errors (500 errors, rate limiting)
- Build and deployment issues (TypeScript errors, environment variables)
- Database and caching (Redis connection, cache fallback)
- Debugging tools and techniques

### [Component Library](component-library.md)
Guide to using the WebAdmin component library.

**Topics Covered**:
- Component organization and structure
- Reusable UI components
- Component patterns and best practices
- Styling and theming

### [Error Handling](error-handling.md)
Error handling patterns and practices for the WebAdmin.

**Topics Covered**:
- Client-side error handling
- SDK error transformation
- User-friendly error messages
- Error boundary implementation

### [Responsive Design Patterns](responsive-design-patterns.md)
Responsive design guidelines and patterns.

**Topics Covered**:
- Mobile-first approach
- Breakpoint strategies
- Responsive component patterns
- Performance optimization for mobile

---

## 🚀 Quick Start

### Development Setup

See the main [Development README](../../README.md) for setup instructions.

### Common Development Tasks

**Run Development Server:**
```bash
./scripts/start-dev.sh
```

**Run ESLint:**
```bash
cd WebAdmin
npm run lint
```

**Type Check:**
```bash
cd WebAdmin
npm run type-check
```

**Build SDKs:**
```bash
cd SDKs/Node/Admin && npm run build
cd ../Core && npm run build
```

---

## 🔧 WebAdmin Architecture

### Technology Stack
- **Framework**: Next.js 14 (App Router)
- **Language**: TypeScript
- **UI Library**: Mantine
- **State Management**: Zustand
- **Authentication**: Clerk
- **Real-time**: SignalR
- **API Clients**: Generated SDKs (@knn_labs/conduit-gateway-client, @knn_labs/conduit-admin-client)

### Key Directories

```
WebAdmin/
├── src/
│   ├── app/              # Next.js app router pages
│   ├── components/       # React components
│   ├── lib/             # Utilities and helpers
│   │   ├── api/         # API client setup
│   │   ├── auth/        # Authentication utilities
│   │   ├── utils/       # General utilities
│   │   └── hooks/       # Custom React hooks
│   ├── stores/          # Zustand stores
│   └── types/           # TypeScript type definitions
├── public/              # Static assets
└── .env.local           # Environment variables (local)
```

---

## 🐛 Getting Help

### Troubleshooting

1. **Check [Troubleshooting Guide](troubleshooting.md)** first
2. **Enable debug logging** in your browser console
3. **Check Docker logs** for API errors
4. **Verify environment variables** are set correctly

### Common Issues

**Cannot connect to API:**
- Verify APIs are running: `docker ps`
- Check environment variables in `.env.local`
- Ensure Docker network is configured correctly

**SignalR not connecting:**
- Check WebSocket support in browser
- Verify firewall/proxy settings
- Enable fallback to polling mode

**Build errors:**
- Run `npm run lint` to check for ESLint errors
- Run `npm run type-check` to verify TypeScript types
- Clear `.next` directory and rebuild

---

## 📖 Related Documentation

- **[Development Guide](../README.md)** - General development documentation
- **[API Guides](../../api-guides/README.md)** - API integration guides
- **[Architecture](../../architecture/README.md)** - System architecture documentation

---

*For questions or clarifications about WebAdmin development, please refer to the troubleshooting guide or create an issue in the repository.*
