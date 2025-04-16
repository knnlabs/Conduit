![ConduitLLM Logo](docs/assets/conduit.png)

# ConduitLLM

## Overview

ConduitLLM is a unified, modular, and extensible platform designed to simplify interaction with multiple Large Language Models (LLMs). It provides a single, consistent OpenAI-compatible REST API endpoint, acting as a gateway or "conduit" to various LLM backends such as OpenAI, Anthropic, Azure OpenAI, Google Gemini, Cohere, and others.

Built with .NET and designed for containerization (Docker), ConduitLLM aims to streamline the development, deployment, and management of LLM-powered applications by abstracting provider-specific complexities.

## Key Features

-   **OpenAI-Compatible REST API**: Exposes a standard `/v1/chat/completions` endpoint for seamless integration with existing tools and SDKs.
-   **Multi-Provider Support**: Interact with various LLM providers through a single interface.
-   **Model Routing & Mapping**: Define custom model aliases (e.g., `my-gpt4`) and map them to specific provider models (e.g., `openai/gpt-4`). Supports routing strategies (e.g., round-robin, least used).
-   **Virtual API Key Management**: Create and manage Conduit-specific API keys (`condt_...`) with built-in spend tracking and access control, decoupling client applications from underlying provider keys.
-   **Streaming & Non-Streaming**: Supports both standard request/response and streaming for real-time interactions.
-   **Web-Based User Interface (WebUI)**: An administrative dashboard for managing configuration (providers, keys, models), viewing usage statistics, and interacting directly with models.
-   **Centralized Configuration**: Flexible configuration options via database, environment variables, or `appsettings.json` files.
-   **Extensible Architecture**: Easily add support for new LLM providers.
-   **Container-Friendly**: Designed for deployment using Docker.

## Architecture & Components

ConduitLLM follows a modular architecture, with distinct projects handling specific responsibilities:

```mermaid
graph LR
    subgraph "User/Client Interaction"
        direction LR
        Client[WebUI / Client App]
    end

    subgraph "ConduitLLM System"
        direction LR
        Http[ConduitLLM.Http <br>(API Gateway)]
        Core[ConduitLLM.Core <br>(Orchestration)]
        Providers[ConduitLLM.Providers <br>(Provider Logic)]
        Config[ConduitLLM.Configuration <br>(Settings & Management)]

        Client --> Http
        Http --> Core
        Core --> Providers

        Http --> Config
        Core --> Config
        Providers --> Config
    end

    subgraph "External Services"
        direction LR
        LLM[LLM Backends <br>(OpenAI, Anthropic, etc.)]
    end

    Providers --> LLM

    style Client fill:#f9f,stroke:#333,stroke-width:2px
    style LLM fill:#ccf,stroke:#333,stroke-width:2px
    style Config fill:#ff9,stroke:#333,stroke-width:1px,stroke-dasharray: 5 5
```

-   **`ConduitLLM.Http`**: The main ASP.NET Core application exposing the public-facing OpenAI-compatible REST API. Handles request validation, authentication (provider & virtual keys), and forwards requests to the core logic.
-   **`ConduitLLM.WebUI`**: A Blazor-based web application providing an administrative interface to manage providers, virtual keys, model mappings, view logs/stats, and perform test completions. Communicates with `ConduitLLM.Http`.
-   **`ConduitLLM.Core`**: Contains the central orchestration logic, interfaces (`ILLMClient`, `ILLMRouter`), request/response models, and routing strategies. It acts as the bridge between the API layer and the provider implementations.
-   **`ConduitLLM.Providers`**: Implements the specific client logic required to interact with each supported LLM provider's API. New providers are added here.
-   **`ConduitLLM.Configuration`**: Manages loading configuration from various sources (database, environment variables, JSON files) and provides strongly-typed settings objects and dependency injection extensions used across the solution.
-   **`ConduitLLM.Tests`**: Contains xUnit tests (unit and integration) covering all major components of the solution.
-   **`ConduitLLM.Examples`**: Includes sample client applications and scripts demonstrating how to interact with the ConduitLLM API.

## Getting Started

### Prerequisites

-   .NET 9.0 SDK (or the version specified in `global.json` / `.csproj` files)
-   (Optional) Docker Desktop for containerized deployment

### Running the Solution

The easiest way to run the entire solution (API and WebUI) is using the provided shell scripts:

1.  **Configure Providers**: Add your LLM provider API keys and desired model mappings. This can be done via:
    *   Environment variables (see `docs/Environment-Variables.md`).
    *   Editing `ConduitLLM.Http/appsettings.json` (primarily for local development).
    *   Using the WebUI after the initial startup (configuration is persisted in `configuration.db`).
2.  **Start Services**:
    ```bash
    ./start.sh
    ```
    This script builds the necessary projects and launches both the `ConduitLLM.Http` API and the `ConduitLLM.WebUI` in the background.

3.  **Access Services**:
    *   **API**: `http://localhost:5000` / `https://localhost:5003`
    *   **WebUI**: `http://localhost:5001` / `https://localhost:5002`
    *   **API Swagger Docs** (Development Mode): `http://localhost:5000/swagger`

4.  **Stop Services**:
    ```bash
    ./stop.sh
    ```

Alternatively, you can run individual projects using `dotnet run`:

```bash
# Example: Run the API server only
export HttpApiHttpPort=5000
export HttpApiHttpsPort=5003
dotnet run --project ConduitLLM.Http
```

Refer to the README files within each sub-project directory for more specific build/run instructions.

## Configuration

ConduitLLM offers flexible configuration options:

-   **Environment Variables**: Ideal for containerized deployments and secrets management. See `docs/Environment-Variables.md`.
-   **`appsettings.json`**: Located in `ConduitLLM.Http` and `ConduitLLM.WebUI` for base settings.
-   **Database (`configuration.db`)**: The primary source for dynamic configuration like provider credentials, model mappings, and virtual keys, managed via the WebUI or direct database access.

See `ConduitLLM.Configuration/README.md` and `docs/Configuration-Guide.md` for detailed information.

## Usage

Interact with ConduitLLM via:

1.  **REST API**: Make requests to `POST /v1/chat/completions` using an HTTP client or OpenAI SDKs. Authenticate using either a direct provider API key or a Conduit-generated virtual key in the `Authorization: Bearer <key>` header.
2.  **WebUI**: Use the administrative interface (`http://localhost:5001`) to manage settings and perform test completions.

## Documentation

For more detailed information on specific features, refer to the `docs/` directory:

-   `docs/Architecture-Overview.md`
-   `docs/Getting-Started.md`
-   `docs/Configuration-Guide.md`
-   `docs/Provider-Integration.md`
-   `docs/LLM-Routing.md`
-   `docs/Virtual-Keys.md`
-   `docs/WebUI-Guide.md`
-   ... and others.

## Contributing

Contributions are welcome! Please refer to the contribution guidelines (if available) or open an issue/pull request on the project repository.

## License

This project is licensed under the terms specified in the `LICENSE` file (if present) at the root of the repository.
