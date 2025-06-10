#\!/bin/bash
# Run integration tests with actual database connection

# Export the connection string for the tests
export ConnectionStrings__DefaultConnection="Host=localhost;Database=conduitllm;Username=conduitllm;Password=conduitllm"
export CONDUIT_MASTER_KEY="test-master-key"

# Run only the integration tests
dotnet test --filter "FullyQualifiedName~Integration" --no-build -v normal
