# Conduit SDKs

This directory contains SDKs for the Conduit API across multiple programming languages and platforms.

## 📁 Directory Structure

```
SDKs/
├── Node/                   # Node.js/TypeScript SDKs
│   ├── Common/            # Shared public SDK utilities
│   └── Gateway/           # Gateway API SDK
├── Python/                # Python SDKs (planned)
│   ├── Admin/
│   ├── Core/
│   └── Realtime/
├── Go/                    # Go SDKs (planned)
│   ├── Admin/
│   ├── Core/
│   └── Realtime/
├── DotNet/                # .NET SDKs (planned)
│   ├── Admin/
│   ├── Core/
│   └── Realtime/
└── README.md              # This file
```

## 🚀 Available SDKs

### Node.js/TypeScript
- **Gateway API SDK** (`SDKs/Node/Gateway/`) - ✅ Available
  - **NPM Package**: `@knn_labs/conduit-gateway-client`
  - **Installation**: `npm install @knn_labs/conduit-gateway-client`
- **Common utilities** (`SDKs/Node/Common/`) - ✅ Available for SDK consumers

The former Admin Node SDK is retired from future releases. Admin API consumers should generate a
client from `Services/ConduitLLM.Admin/openapi-admin.json` or use the contract directly.

### Python (Planned)
- **Admin API SDK** - 🔄 Coming Soon
- **Gateway API SDK** - 🔄 Coming Soon
- **Realtime SDK** - 🔄 Coming Soon

### Go (Planned)
- **Admin API SDK** - 🔄 Coming Soon
- **Gateway API SDK** - 🔄 Coming Soon
- **Realtime SDK** - 🔄 Coming Soon

### .NET (Planned)
- **Admin API SDK** - 🔄 Coming Soon
- **Gateway API SDK** - 🔄 Coming Soon
- **Realtime SDK** - 🔄 Coming Soon

## 🎯 API Types

### Admin API
- Manage virtual keys and API access
- Configure providers and model mappings
- Monitor usage and costs
- System administration and health checks

### Gateway API (Planned)
- Chat completions and text generation
- Embeddings and vector operations
- Image generation and vision
- Audio processing and transcription

### Realtime API (Planned)
- WebSocket connections for real-time communication
- Streaming responses and live updates
- Real-time collaboration features

## 🔧 Development Guidelines

### Adding New SDKs

1. **Choose the appropriate directory** (`Node/`, `Python/`, `Go/`, `DotNet/`)
2. **Create service subdirectory** (`Admin/`, `Core/`, `Realtime/`)
3. **Follow platform conventions** for package structure and naming
4. **Include comprehensive documentation** and usage examples
5. **Add appropriate testing** and CI/CD pipelines
6. **Update this README** with the new SDK information

### Platform-Specific Guidelines

- **Node.js**: Use TypeScript, follow npm conventions, include `.d.ts` files
- **Python**: Follow PEP conventions, include type hints, use setuptools
- **Go**: Follow Go module conventions, include proper documentation
- **C#/.NET**: Follow .NET conventions, include XML documentation

## 📚 Documentation

Each SDK includes:
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

- **Issues**: [GitHub Issues](https://github.com/nickna/Conduit/issues)
- **Documentation**: [Conduit Docs](https://github.com/nickna/Conduit/docs)
- **Discord**: [Community Discord](https://discord.gg/conduit) (if available)

---

*This multi-platform approach ensures developers can use Conduit with their preferred programming language while maintaining consistency across all SDKs.*
