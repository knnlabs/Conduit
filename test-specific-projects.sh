#!/bin/bash

# Script to run tests for specific projects, excluding integration tests

echo "Running unit tests (excluding integration tests)..."

# Run tests for specific projects that don't require containers
dotnet test ConduitLLM.Tests/ConduitLLM.Tests.csproj --no-build
dotnet test ConduitLLM.Admin.Tests/ConduitLLM.Admin.Tests.csproj --no-build
dotnet test ConduitLLM.Http.Tests/ConduitLLM.Http.Tests.csproj --no-build
dotnet test ConduitLLM.Configuration.Tests/ConduitLLM.Configuration.Tests.csproj --no-build

echo "Unit tests completed!"
echo ""
echo "Note: Integration tests were skipped because they require Docker containers."
echo "To run integration tests separately, use:"
echo "  dotnet test ConduitLLM.IntegrationTests/ConduitLLM.IntegrationTests.csproj"