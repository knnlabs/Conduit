# ConduitLLM.Core

## Overview

**ConduitLLM.Core** is the core library of the ConduitLLM system, part of the larger [Conduit](../Conduit.sln) solution. It provides the foundational logic for interacting with multiple Large Language Model (LLM) providers, model routing, and orchestration. Other components of the Conduit solution (such as API services and the WebUI) depend on this library for all LLM-related operations.

## How It Fits in Conduit

- **ConduitLLM.Core**: Core logic, interfaces, model definitions, routing, and provider abstraction.
- **ConduitLLM.Http**: API layer for external access, depends on Core.
- **ConduitLLM.WebUI**: Web interface, communicates with the API, depends on Core via API.
- **ConduitLLM.Configuration**: Configuration logic and shared config models, referenced by Core.

## Key Features

- **Unified LLM Abstraction**: Interact with multiple LLM providers via a single API.
- **Routing & Strategy**: Supports routing strategies (e.g., round robin, least used) for load balancing or fallback.
- **Extensible**: Add new providers by implementing interfaces.

## Main Components

- `Conduit`: The primary entry point for LLM operations. Handles chat completions, streaming, and routing.
- `Interfaces/`: Defines contracts for LLM clients, routers, and factories.
- `Models/`: Request and response models for LLM operations.
- `Exceptions/`: Custom exception types for error handling.
- `Routing/`: Routing logic and strategies.
- `Repositories/`, `Services/`, `Caching/`: Support for advanced features and extensibility.

## Database Helper

The `DbConnectionHelper` class provides unified detection and parsing of database configuration via environment variables:

- **Postgres:**
  - Set `DATABASE_URL` (e.g., `postgresql://user:password@host:5432/database`)
- **SQLite:**
  - Set `CONDUIT_SQLITE_PATH` (e.g., `/data/ConduitConfig.db`)

No other DB-related variables are needed. The helper will auto-detect the provider and parse the connection string for use with EF Core.

## Usage

Typically, you do not use `ConduitLLM.Core` directly. Instead, reference it from API or service projects. However, for direct use:

```csharp
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;

var factory = /* your ILLMClientFactory implementation */;
var router = /* optional ILLMRouter implementation */;
var conduit = new Conduit(factory, router);

var request = new ChatCompletionRequest { Model = "router:roundrobin:gpt-4", ... };
var response = await conduit.CreateChatCompletionAsync(request);
```

## Routing & Model Aliases

- To use routing, prefix your model name with `router:` (e.g., `router:roundrobin:gpt-4`).
- Supported strategies: `simple`, `random`, `roundrobin`, `leastused`, `passthrough`.
- If no router is configured, calls are sent directly to the specified model/provider.

## Configuration

`ConduitLLM.Core` itself does not require direct configuration, but expects:

- **Provider configuration**: Set up in the referencing project (usually via `ConduitLLM.Configuration`).
- **Dependency injection**: Register your implementations of `ILLMClientFactory` and (optionally) `ILLMRouter`.

## Dependencies

- [.NET 9.0](https://dotnet.microsoft.com/)
- Microsoft.Extensions.Options
- Microsoft.Extensions.Logging.Abstractions
- Reference to `ConduitLLM.Configuration`

## Extending

To add a new LLM provider:

1. Implement `ILLMClient` for your provider.
2. Register your client in your `ILLMClientFactory` implementation.
3. (Optional) Add routing logic by implementing `ILLMRouter`.

## Development

- Build as part of the `Conduit.sln` solution.
- Unit tests and integration tests should be placed in the appropriate test projects.

## License

See the root of the repository for license information.
