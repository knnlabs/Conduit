# WebUI Tests

This directory contains tests for the ConduitLLM WebUI project.

## Test Files

### PageCompilationTests.cs
Basic tests that verify all Blazor pages in the WebUI project can be compiled and instantiated without errors. These tests:

- **AllPagesInWebUI_CanBeInstantiated**: Discovers all page components dynamically and verifies they can be instantiated
- **Page_CanBeInstantiated**: Tests specific pages individually to ensure they can be created
- **AllPages_HaveProperNaming**: Verifies all pages follow proper naming conventions
- **AllPages_ArePublicClasses**: Ensures all page components are public classes

## What These Tests Verify

While basic, these tests provide important coverage:

1. **Compilation Errors**: If a page has syntax errors or missing dependencies, it will fail to instantiate
2. **Constructor Issues**: Pages with problematic constructors will be caught
3. **Type Safety**: Ensures all pages properly inherit from ComponentBase
4. **Naming Conventions**: Verifies consistent naming patterns are followed

## Running the Tests

```bash
# Run all WebUI tests
dotnet test --filter "Namespace~ConduitLLM.Tests.WebUI"

# Run just the page compilation tests
dotnet test --filter "FullyQualifiedName~PageCompilationTests"
```

## Future Improvements

For more comprehensive testing, consider adding:

1. **Integration Tests**: Use WebApplicationFactory to test actual HTTP requests to pages
2. **Component Tests**: Use bUnit to test individual Blazor components with mocked services
3. **Authentication Tests**: Verify pages properly enforce authentication requirements
4. **Error Handling Tests**: Ensure pages handle errors gracefully
5. **Performance Tests**: Verify pages load within acceptable time limits