# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands
- Build solution: `dotnet build`
- Run tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"`
- Start API server: `dotnet run --project ConduitLLM.Http`
- Start web UI: `dotnet run --project ConduitLLM.WebUI`
- Start both services: `docker compose up -d`

## Code Style Guidelines
- **Naming**: 
  - Interfaces prefixed with 'I' (e.g., `ILLMClient`)
  - Async methods suffixed with 'Async'
  - Private fields prefixed with underscore (`_logger`)
  - Use PascalCase for public members, camelCase for parameters
- **Formatting**:
  - 4 spaces for indentation
  - Opening braces on new line (Allman style)
  - Max line length ~100 characters
- **Error Handling**:
  - Use custom exception types inheriting from base exceptions
  - Include contextual information in exception messages
  - Use try/catch with appropriate logging
- **Testing**:
  - Test methods follow pattern: `MethodName_Condition_ExpectedResult`
  - One assertion per test is preferred
  - Use Moq for mocking dependencies

## XML Documentation Standards

- **All public APIs** should have comprehensive XML documentation.
- **Add XML comments** to the following code elements:
  - Classes and interfaces
  - Public properties and methods
  - Public enum values
  - Non-obvious public fields
  - Important private methods that implement complex logic

- **XML Tags to Use**:
  - `<summary>` - Required for all documented elements
  - `<param>` - Required for all method parameters
  - `<returns>` - Required for non-void methods
  - `<exception>` - Document exceptions thrown by methods
  - `<remarks>` - Add additional details beyond the summary
  - `<example>` - Add usage examples where helpful
  - `<see>` / `<seealso>` - Cross-reference related classes or methods

- **Documentation Quality**:
  - **Summaries** should be brief, concise descriptions (1-2 sentences)
  - **Include 'why' information** in addition to 'what' when appropriate
  - Use **complete sentences** ending with periods
  - Be **specific about parameter roles** and constraints
  - Document **side effects** such as state changes
  - Note **thread safety** considerations for multi-threaded code
  - Include **performance characteristics** for performance-critical code

- **API Documentation**:
  - Controllers and DTOs should include detailed response documentation
  - Include `<response>` tags with HTTP status codes and response descriptions
  - Document serialization attributes and their effects

- **Example Format**:

```csharp
/// <summary>
/// Authenticates a user and generates an access token.
/// </summary>
/// <param name="username">The user's login name.</param>
/// <param name="password">The user's password.</param>
/// <param name="rememberMe">Whether to extend the token validity period.</param>
/// <returns>A JWT token string if authentication is successful.</returns>
/// <exception cref="ArgumentException">Thrown when username or password is empty.</exception>
/// <exception cref="AuthenticationException">Thrown when credentials are invalid.</exception>
/// <remarks>
/// The token validity period depends on the rememberMe parameter:
/// - If true: 30-day validity
/// - If false: 24-hour validity
/// </remarks>
public async Task<string> AuthenticateAsync(string username, string password, bool rememberMe)
{
    // Method implementation
}
```

## Documentation Coverage

To check XML documentation coverage across the solution, use the provided XML documentation coverage checker:

```bash
# Run the documentation coverage check
./check-documentation.sh
```

The coverage checker will:
- Scan the solution for C# files
- Identify undocumented and partially documented types
- Generate a report with documentation coverage statistics
- Provide recommendations for documentation improvements

The tool is located in the `tools/XmlDocumentationChecker` directory.

### Coverage Priorities

Focus documentation efforts in this order:
1. **Core interfaces** - Foundation of the architecture
2. **Provider implementations** - Implementation of core interfaces for different LLM providers
3. **DTO and Model classes** - Data structures used across the application
4. **Controller classes** - Public API endpoints
5. **Service classes** - Business logic implementation
6. **Helper and utility classes** - Supporting functionality