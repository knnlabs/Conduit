# ConduitLLM.Configuration

## Overview

**ConduitLLM.Configuration** is a core library within the [Conduit.sln](../Conduit.sln) solution. Its primary role is to centralize and standardize configuration logic, settings, and extension methods that are shared across the various ConduitLLM projects, such as the API server, WebUI, and background services.

By encapsulating configuration concerns in a single project, ConduitLLM.Configuration ensures consistency, maintainability, and ease of deployment for the entire ConduitLLM ecosystem.

---

## How It Fits Into the Conduit Solution

The Conduit solution is a modular, scalable platform for large language model (LLM) orchestration, featuring components for API serving, web-based UI, caching, and more. `ConduitLLM.Configuration` is referenced by other projects in the solution to:

- Provide strongly-typed configuration objects (e.g., for caching, LLM providers, API keys, service endpoints).
- Register and configure services via extension methods (e.g., for dependency injection).
- Centralize environment variable and appsettings management.
- Ensure all ConduitLLM components follow the same configuration patterns.

---

## Key Features

- **Extension Methods:** Utilities for registering and configuring services (e.g., caching, LLM clients) in ASP.NET Core dependency injection.
- **Strongly-Typed Settings:** Classes that map to configuration sections (from `appsettings.json` or environment variables) for safe, discoverable access.
- **Environment Variable Support:** Reads and applies configuration from environment variables, supporting containerized and cloud deployments.
- **Shared Conventions:** Enforces consistent configuration keys, section names, and patterns across the solution.

---

## Usage

### Referencing in Other Projects

Add a project reference to `ConduitLLM.Configuration` from any ConduitLLM project that requires shared configuration logic.

```xml
<ItemGroup>
  <ProjectReference Include="..\ConduitLLM.Configuration\ConduitLLM.Configuration.csproj" />
</ItemGroup>
```

### Registering Configuration in ASP.NET Core

In your `Program.cs` or `Startup.cs`, use the provided extension methods to register configuration and services. For example:

```csharp
using ConduitLLM.Configuration.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register LLM caching with configuration
builder.Services.AddLLMCaching(builder.Configuration);

// Register other shared services
builder.Services.AddConduitLLMConfiguration(builder.Configuration);
```

### Configuration Sources

- **appsettings.json:** Place configuration sections here for local development.
- **Environment Variables:** Override or provide configuration in production or containerized environments.

#### Example: Environment Variable Usage

```bash
export LLM__Provider=OpenAI
export LLM__ApiKey=your-api-key
export Cache__Enabled=true
```

---

## Configuration Sections

Common configuration sections managed by this project include (but are not limited to):

- `LLM`: Settings for LLM provider, API keys, model selection, etc.
- `Cache`: Caching options and backend configuration.
- `ServiceEndpoints`: URLs and ports for internal/external services.

---

## Best Practices

- **Single Source of Truth:** Always define shared configuration logic in this project to avoid duplication.
- **Use Strongly-Typed Classes:** Access configuration via injected options classes for safety and discoverability.
- **Environment Overrides:** Prefer environment variables for secrets and deployment-specific settings.

---

## Contributing

If you are adding new configuration logic or extension methods, ensure they are generic, reusable, and well-documented. Update this README as needed.

---

## License

This project is part of the ConduitLLM solution and inherits its license.
