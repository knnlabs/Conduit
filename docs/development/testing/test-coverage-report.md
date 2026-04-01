# Test Coverage

## Running Tests

```bash
# All tests
dotnet test

# Specific test project
dotnet test ConduitLLM.Tests

# Specific test
dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"

# With coverage (requires coverlet)
dotnet test --collect:"XPlat Code Coverage"
```

## Coverage Tooling

See [Coverage Setup](./coverage.md) for details on the Coverlet + ReportGenerator pipeline and CI integration.

## Testing Patterns

The codebase uses **xUnit + Moq + FluentAssertions**:

- Test naming: `MethodName_Condition_ExpectedResult`
- One assertion per test (preferred)
- `TestBase` classes for common setup
- Category traits for test organization

See [CLAUDE.md](../../../CLAUDE.md#testing) and [Mutation Testing Guide](./Mutation-Testing-Guide.md) for additional testing guidance.
