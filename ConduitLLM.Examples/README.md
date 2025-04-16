# ConduitLLM.Examples

## Overview

`ConduitLLM.Examples` is a collection of example projects and scripts that demonstrate how to use the core features of ConduitLLM, a modular, container-friendly .NET solution for serving and interacting with Large Language Models (LLMs). This directory is part of the broader [Conduit.sln](../Conduit.sln) solution, designed to provide a unified, extensible platform for LLM-powered applications.

## How It Fits Into the Conduit Solution

- **Conduit.sln**: The main solution file that aggregates all ConduitLLM projects, including core libraries, API servers, WebUI, and this Examples directory.
- **ConduitLLM.Examples**: Provides ready-to-run samples and integration patterns for developers, showcasing how to interact with the ConduitLLM HTTP API, WebUI, and configuration system.
- **Other Projects**: Works alongside `ConduitLLM.Http` (API server), `ConduitLLM.WebUI` (web interface), and shared configuration libraries.

## What’s Included

- **Sample Clients**: Example .NET clients that consume the ConduitLLM HTTP API.
- **Integration Scripts**: Scripts for automating requests, running batch jobs, or demonstrating advanced features like streaming.
- **Configuration Samples**: Example configuration files and environment variable setups to help you get started quickly.

## Getting Started

### Prerequisites

- .NET 7.0 SDK or later
- ConduitLLM core services running (see root README for setup)
- (Optional) Docker, if running services in containers

### Running Examples

1. **Build the Solution**  
   From the root directory:
   ```bash
   dotnet build Conduit.sln
   ```

2. **Start Core Services**  
   Ensure `ConduitLLM.Http` and/or `ConduitLLM.WebUI` are running.  
   Use the provided `start.sh` script or run via Visual Studio.

3. **Run an Example**  
   Navigate to the `ConduitLLM.Examples` directory and run a sample:
   ```bash
   cd ConduitLLM.Examples
   dotnet run --project ExampleClient
   ```

   Replace `ExampleClient` with the desired example project.

### Configuration

Examples are designed to work out-of-the-box with the default ConduitLLM configuration.  
If you have custom ports or endpoints, set the following environment variables before running examples:

- `HttpApiHttpPort` (default: 5000)
- `HttpApiHttpsPort` (default: 5003)
- `WebUIHttpPort` (default: 5001)
- `WebUIHttpsPort` (default: 5002)

You can export these variables in your shell or set them in your IDE.

### Example

```bash
export HttpApiHttpPort=6000
dotnet run --project ExampleClient
```

## Directory Structure

```
ConduitLLM.Examples/
├── ExampleClient/        # .NET client consuming ConduitLLM API
├── BatchScript/          # Batch processing example
├── README.md             # This file
└── ...                   # Additional samples and scripts
```

## Contributing

Contributions are welcome! If you have a new example or integration pattern, feel free to submit a pull request.

## Troubleshooting

- Ensure all core ConduitLLM services are running and reachable from your example.
- Check port configurations if you encounter connection errors.
- Review environment variable settings for custom deployments.

## License

This project is licensed under the MIT License.
