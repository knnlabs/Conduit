# ConduitLLM.Tests

## Overview
`ConduitLLM.Tests` is the automated test suite for the ConduitLLM solution, which is managed via the top-level `Conduit.sln` file. This project ensures the reliability, correctness, and maintainability of the core ConduitLLM components by providing comprehensive unit and integration tests.

## How It Fits Into the Conduit Solution
The ConduitLLM solution is a modular .NET-based framework for working with Large Language Models (LLMs). It is composed of several sub-projects, including:

- **ConduitLLM.Core**: Core abstractions, interfaces, and shared logic for LLM operations.
- **ConduitLLM.Http**: Provides HTTP APIs for LLM operations.
- **ConduitLLM.WebUI**: Web-based user interface for interacting with LLMs.
- **ConduitLLM.Configuration**: Centralized configuration management.
- **ConduitLLM.Providers**: Integrations for various LLM providers (OpenAI, Cohere, Gemini, Anthropic, LiteLLM, etc.).
- **ConduitLLM.Tests**: This project. Contains tests for all core, provider, HTTP, and configuration components.

All these projects are orchestrated via `Conduit.sln`, allowing for coordinated development and testing.

## What Does ConduitLLM.Tests Do?
- **Covers all major components**: Tests core logic, provider integrations, API endpoints, configuration, caching, streaming, security, and middleware.
- **Ensures provider compatibility**: Validates correct interaction with OpenAI, Cohere, Gemini, Anthropic, and LiteLLM clients.
- **Supports CI/CD**: Can be run as part of automated pipelines to prevent regressions.

## Test Structure
The test project is organized as follows:

- `*.cs` files: Test suites for specific providers or features (e.g., `OpenAIClientTests.cs`, `GeminiClientTests.cs`, `StreamingTests.cs`).
- `Caching/`, `Middleware/`, `Security/`, `Services/`, `TestHelpers/`: Subdirectories for organizing tests and helpers by concern.

## How to Run the Tests
You can run all tests using the .NET CLI:

```bash
# From the solution root
dotnet test ConduitLLM.Tests/ConduitLLM.Tests.csproj
```

Or run all tests in the solution:

```bash
dotnet test Conduit.sln
```

## Configuration
Most tests run with default settings and use in-memory or mock services. However, some integration tests may expect certain environment variables or configuration files to be set, especially when testing real LLM provider APIs. See the following for guidance:

- **Environment Variables**: Provider API keys (e.g., `OPENAI_API_KEY`, `COHERE_API_KEY`, etc.) may be required for integration tests.
- **Ports**: The test suite does not bind to HTTP/HTTPS ports by default, but if you run the full solution, refer to the main project documentation for port configuration.

## Adding or Modifying Tests
- Place new test files in the appropriate directory or create a new one if needed.
- Follow the naming convention: `[Component]Tests.cs`.
- Use xUnit (or the test framework specified in `.csproj`).
- Use mocks or stubs for external dependencies where possible.

## Best Practices
- Keep tests isolated and repeatable.
- Clean up any resources created during tests.
- Prefer in-memory or mock services for unit tests; use real services only for explicit integration tests.

## Additional Information
- For details on the core logic, see `ConduitLLM.Core/README.md`.
- For provider-specific info, see `ConduitLLM.Providers/README.md`.
- For API usage, see `ConduitLLM.Http/README.md`.
- For configuration, see `ConduitLLM.Configuration/README.md`.

## Contact & Support
For questions, issues, or contributions, please refer to the main ConduitLLM repository documentation or open an issue on the project's GitHub page.
