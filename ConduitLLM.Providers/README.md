# ConduitLLM.Providers

## Overview

**ConduitLLM.Providers** is a core library within the [ConduitLLM](../) solution, responsible for providing modular, extensible integration with various Large Language Model (LLM) backends and services. It abstracts the details of interacting with different LLM providers and exposes a uniform interface for the rest of the Conduit ecosystem.

This project is part of the larger `Conduit.sln` solution, which is composed of several sub-projects:

- **ConduitLLM.Http**: The HTTP API layer that exposes LLM functionality via REST endpoints.
- **ConduitLLM.WebUI**: The web-based user interface for interacting with LLMs.
- **ConduitLLM.Configuration**: Centralized configuration and settings management.
- **ConduitLLM.Providers**: (This project) LLM provider abstraction and implementations.
- **Other sub-projects**: May include Caching, Utilities, or Domain-specific libraries.

## How It Fits Into Conduit

The Providers library is consumed by both the API and UI layers. It allows the Conduit solution to support multiple LLM backends (e.g., OpenAI, Azure OpenAI, HuggingFace, local models) without requiring changes in the API or UI code. New providers can be added by implementing the appropriate interfaces.

```
[WebUI] <---> [Http API] <---> [Providers] <---> [LLM Backends]
                             ^
                             |
                    [Configuration]
```

## Features

- Unified interface for LLM operations (completion, chat, embeddings, etc.)
- Plug-and-play support for multiple LLM vendors
- Extensible: add custom providers by implementing interfaces
- Centralized provider configuration (via ConduitLLM.Configuration)
- Dependency injection friendly

## Usage

### Adding a Provider

To add a new provider, implement the relevant interfaces (e.g., `ILLMProvider`, `IChatProvider`) and register your implementation in the DI container (typically in the API or startup project).

### Example: Registering Providers

```csharp
services.AddLLMProviders(Configuration);
```

This will scan and register all available providers based on your configuration.

### Consuming a Provider

Inject the desired interface into your service or controller:

```csharp
public class MyService
{
    private readonly ILLMProvider _llmProvider;
    public MyService(ILLMProvider llmProvider) { _llmProvider = llmProvider; }

    public async Task<string> GetCompletion(string prompt)
    {
        return await _llmProvider.CompleteAsync(prompt);
    }
}
```

## Configuration

Provider selection and credentials are configured via environment variables or configuration files, typically managed by the `ConduitLLM.Configuration` project.

Example environment variables (names may vary by provider):

- `OpenAI__ApiKey`
- `AzureOpenAI__Endpoint`
- `HuggingFace__Token`
- `DefaultProvider=OpenAI`

You can set these in your environment, `appsettings.json`, or through Docker Compose.

## Extending

To add a new provider:

1. Implement the provider interface(s).
2. Register the provider in the DI container.
3. Add configuration schema in `ConduitLLM.Configuration` if needed.

## Dependencies

- .NET 7.0+ (or as specified by the solution)
- Dependency Injection (Microsoft.Extensions.DependencyInjection)
- Configuration (Microsoft.Extensions.Configuration)

## Development

- All code should be unit tested.
- Follow the project’s code style and contribution guidelines.
- PRs for new providers should include documentation and tests.

## License

This project is part of the ConduitLLM solution and inherits its license.
