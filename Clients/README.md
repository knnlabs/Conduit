# Conduit Client Libraries

This directory contains client libraries for the Conduit API across multiple programming languages and platforms.

## 📁 Directory Structure

```
Clients/
├── Node/                   # Node.js/TypeScript clients
│   ├── Admin/             # Admin API client
│   ├── Core/              # Core API client (planned)
│   └── Realtime/          # Realtime/WebSocket client (planned)
├── Python/                # Python clients (planned)
│   ├── Admin/
│   ├── Core/
│   └── Realtime/
├── Go/                    # Go clients (planned)
│   ├── Admin/
│   ├── Core/
│   └── Realtime/
├── DotNet/                # .NET clients (planned)
│   ├── Admin/
│   ├── Core/
│   └── Realtime/
└── README.md              # This file
```

## 🚀 Available Clients

### Node.js/TypeScript
- **Admin API Client** (`Clients/Node/Admin/`) - ✅ Available
  - Full TypeScript support with comprehensive type definitions
  - Automatic retry logic and error handling
  - Caching support for improved performance
  - Environment-based configuration
  - **NPM Package**: `@conduit/admin-client`
  - **Installation**: `npm install @conduit/admin-client`

### Python (Planned)
- **Admin API Client** - 🔄 Coming Soon
- **Core API Client** - 🔄 Coming Soon
- **Realtime Client** - 🔄 Coming Soon

### Go (Planned)
- **Admin API Client** - 🔄 Coming Soon
- **Core API Client** - 🔄 Coming Soon
- **Realtime Client** - 🔄 Coming Soon

### .NET (Planned)
- **Admin API Client** - 🔄 Coming Soon
- **Core API Client** - 🔄 Coming Soon
- **Realtime Client** - 🔄 Coming Soon

## 🎯 API Types

### Admin API
- Manage virtual keys and API access
- Configure providers and model mappings
- Monitor usage and costs
- System administration and health checks

### Core API (Planned)
- Chat completions and text generation
- Embeddings and vector operations
- Image generation and vision
- Audio processing and transcription

### Realtime API (Planned)
- WebSocket connections for real-time communication
- Streaming responses and live updates
- Real-time collaboration features

## 🔧 Development Guidelines

### Adding New Clients

1. **Choose the appropriate directory** (`Node/`, `Python/`, `Go/`, `DotNet/`)
2. **Create service subdirectory** (`Admin/`, `Core/`, `Realtime/`)
3. **Follow platform conventions** for package structure and naming
4. **Include comprehensive documentation** and usage examples
5. **Add appropriate testing** and CI/CD pipelines
6. **Update this README** with the new client information

### Platform-Specific Guidelines

- **Node.js**: Use TypeScript, follow npm conventions, include `.d.ts` files
- **Python**: Follow PEP conventions, include type hints, use setuptools
- **Go**: Follow Go module conventions, include proper documentation
- **C#/.NET**: Follow .NET conventions, include XML documentation

## 📚 Documentation

Each client library includes:
- **README.md** - Installation and basic usage
- **API Documentation** - Comprehensive API reference
- **Examples** - Code samples and common use cases
- **Contributing Guide** - Development setup and contribution guidelines

## 🤝 Contributing

1. Follow the directory structure outlined above
2. Include comprehensive tests for all new functionality
3. Add proper documentation and examples
4. Ensure compatibility with the latest Conduit API version
5. Follow platform-specific coding standards

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/knnlabs/Conduit/issues)
- **Documentation**: [Conduit Docs](https://github.com/knnlabs/Conduit/docs)
- **Discord**: [Community Discord](https://discord.gg/conduit) (if available)

---

*This multi-platform approach ensures developers can use Conduit with their preferred programming language while maintaining consistency across all client libraries.*