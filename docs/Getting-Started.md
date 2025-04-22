# Getting Started with ConduitLLM

## Overview

ConduitLLM is a comprehensive LLM management and routing system that allows you to interact with multiple LLM providers through a unified interface. It provides advanced routing capabilities, virtual key management, and a web-based configuration UI.

## Prerequisites

- .NET 9.0 SDK or later
- SQL Server or SQLite for database storage
- API keys for any LLM providers you plan to use (OpenAI, Anthropic, Cohere, Gemini, Fireworks, OpenRouter)

## Installation

### Option 1: Using the scripts

1. Clone the repository:
   ```bash
   git clone https://github.com/your-org/ConduitLLM.git
   cd ConduitLLM
   ```

2. Run the start script:
   ```bash
   ./start.sh
   ```

### Option 2: Manual setup

1. Clone the repository:
   ```bash
   git clone https://github.com/your-org/ConduitLLM.git
   cd ConduitLLM
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run the WebUI project:
   ```bash
   cd ConduitLLM.WebUI
   dotnet run
   ```

## Initial Configuration

1. Open your browser and navigate to the WebUI.
   - **Local Development (`./start.sh`):** `http://localhost:5001`
   - **Docker/Deployed:** Access via the URL configured in the `CONDUIT_API_BASE_URL` environment variable (e.g., `https://conduit.yourdomain.com`), typically through an HTTPS reverse proxy.

2. Navigate to the Configuration page to set up:
   - LLM providers (API keys and endpoints)
   - Model mappings
   - Global settings including the master key

3. Set up your first provider:
   - Select a provider (e.g., OpenAI)
   - Enter your API key (obtain from the provider's website)
   - Configure any additional provider-specific settings

## Configuring Router

The router allows you to distribute requests across different model deployments:

1. Configure model deployments through the WebUI
2. Select a routing strategy (simple, random, round-robin)
3. Set up fallback configurations between models

## Next Steps

- Explore the [Architecture Overview](Architecture-Overview.md) to understand the system components
- Check the [Configuration Guide](Configuration-Guide.md) for detailed configuration options
- See the [API Reference](API-Reference.md) for available endpoints
- Learn about [Virtual Keys](Virtual-Keys.md) for managing access and budgets
