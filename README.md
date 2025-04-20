![ConduitLLM Logo](docs/assets/conduit.png)

# ConduitLLM

[![OpenAI Compatible](https://img.shields.io/badge/OpenAI-Compatible-brightgreen.svg)](https://platform.openai.com/docs/api-reference)
[![Built with .NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Docker Ready](https://img.shields.io/badge/Docker-Ready-2496ED)](https://www.docker.com/)

> A unified API gateway for multiple LLM providers with OpenAI-compatible endpoints

## Why ConduitLLM?

Are you juggling multiple LLM provider APIs in your applications? ConduitLLM solves this problem by providing:

- **Single Integration Point**: Write your code once, switch LLM providers anytime
- **Vendor Independence**: Avoid lock-in to any single LLM provider
- **Simplified API Management**: Centralized key management and usage tracking
- **Cost Optimization**: Route requests to the most cost-effective or performant models

## Overview

ConduitLLM is a unified, modular, and extensible platform designed to simplify interaction with multiple Large Language Models (LLMs). It provides a single, consistent OpenAI-compatible REST API endpoint, acting as a gateway or "conduit" to various LLM backends such as OpenAI, Anthropic, Azure OpenAI, Google Gemini, Cohere, and others.

Built with .NET and designed for containerization (Docker), ConduitLLM streamlines the development, deployment, and management of LLM-powered applications by abstracting provider-specific complexities.

## Key Features

- **OpenAI-Compatible REST API**: Exposes a standard `/v1/chat/completions` endpoint for seamless integration with existing tools and SDKs
- **Multi-Provider Support**: Interact with various LLM providers through a single interface
- **Model Routing & Mapping**: Define custom model aliases (e.g., `my-gpt4`) and map them to specific provider models (e.g., `openai/gpt-4`)
- **Virtual API Key Management**: Create and manage Conduit-specific API keys (`condt_...`) with built-in spend tracking
- **Streaming Support**: Real-time token streaming for responsive applications
- **Web-Based User Interface**: Administrative dashboard for configuration and monitoring
- **Centralized Configuration**: Flexible configuration via database, environment variables, or JSON files
- **Extensible Architecture**: Easily add support for new LLM providers

## 🏗️ Architecture

ConduitLLM follows a modular architecture with distinct components handling specific responsibilities:

```mermaid
flowchart LR
    Client["WebUI / Client App"]
    Http["ConduitLLM.Http(API Gateway)"]
    Core["ConduitLLM.Core(Orchestration)"]
    Providers["ConduitLLM.Providers(Provider Logic)"]
    Config["ConduitLLM.Configuration(Settings)"]
    LLM["LLM Backends(OpenAI, Anthropic, etc.)"]
    
    Client --> Http
    Http --> Core
    Core --> Providers
    Providers --> LLM
    
    Http --> Config
    Core --> Config
    Providers --> Config
```

### Components

- **ConduitLLM.Http**: OpenAI-compatible REST API gateway handling authentication and request forwarding
- **ConduitLLM.WebUI**: Blazor-based admin interface for configuration and monitoring
- **ConduitLLM.Core**: Central orchestration logic, interfaces, and routing strategies
- **ConduitLLM.Providers**: Provider-specific implementations for different LLM services
- **ConduitLLM.Configuration**: Configuration management across various sources

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- (Optional) Docker Desktop for containerized deployment

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/knnlabs/Conduit.git
   cd Conduit/ConduitLLM.WebUI
   ```

2. **Configure LLM Providers**
   - Add your provider API keys via:
     - Environment variables (see `docs/Environment-Variables.md`)
     - Edit `appsettings.json`
     - Use the WebUI after startup

3. **Start the Services**
   ```bash
   ./start.sh
   ```

4. **Access ConduitLLM**
   - **API**: `http://localhost:5000` / `https://localhost:5003`
   - **WebUI**: `http://localhost:5001` / `https://localhost:5002`
   - **API Docs**: `http://localhost:5000/swagger` (Development Mode)

### Docker Installation

```bash
docker compose up -d
```

## Usage

### Using the API

```bash
# Example: Chat completion request
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_yourvirtualkey" \
  -d '{
    "model": "my-gpt4",
    "messages": [{"role": "user", "content": "Hello, world!"}]
  }'
```

### Using with OpenAI SDKs

```python
# Python example
from openai import OpenAI

client = OpenAI(
    api_key="condt_yourvirtualkey",
    base_url="http://localhost:5000/v1"
)

response = client.chat.completions.create(
    model="my-gpt4",
    messages=[{"role": "user", "content": "Hello, world!"}]
)
```

## Documentation

See the `docs/` directory for detailed documentation:

- [API Reference](docs/API-Reference.md)
- [Architecture Overview](docs/Architecture-Overview.md)
- [Budget Management](docs/Budget-Management.md)
- [Cache Configuration](docs/Cache-Configuration.md)
- [Configuration Guide](docs/Configuration-Guide.md)
- [Dashboard Features](docs/Dashboard-Features.md)
- [Environment Variables](docs/Environment-Variables.md)
- [Getting Started](docs/Getting-Started.md)
- [LLM Routing](docs/LLM-Routing.md)
- [Multimodal Vision Support](docs/Multimodal-Vision-Support.md)
- [Provider Integration](docs/Provider-Integration.md)
- [Virtual Keys](docs/Virtual-Keys.md)
- [WebUI Guide](docs/WebUI-Guide.md)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the terms specified in the `LICENSE` file.
