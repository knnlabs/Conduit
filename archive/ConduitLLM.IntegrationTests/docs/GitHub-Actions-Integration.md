# GitHub Actions Integration for Integration Tests

This document shows how to configure GitHub Actions to run integration tests conditionally.

## Basic Configuration (Default - No Integration Tests)

For standard CI builds, integration tests are automatically skipped:

```yaml
name: Build and Test

on:
  push:
    branches: [ main, dev ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore
      
    - name: Test (integration tests automatically skipped)
      run: dotnet test --no-build --verbosity normal
      # No environment variable needed - integration tests skip by default
```

## With Integration Tests Enabled

For nightly builds or specific workflows where you want to run integration tests:

```yaml
name: Full Test Suite with Integration Tests

on:
  schedule:
    - cron: '0 2 * * *'  # Run at 2 AM UTC daily
  workflow_dispatch:     # Allow manual trigger

jobs:
  integration-test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: conduit_test
          POSTGRES_PASSWORD: conduit_test
          POSTGRES_DB: conduit_test
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432
          
      redis:
        image: redis:7-alpine
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 6379:6379
          
      rabbitmq:
        image: rabbitmq:3-management-alpine
        env:
          RABBITMQ_DEFAULT_USER: conduit_test
          RABBITMQ_DEFAULT_PASS: conduit_test
        ports:
          - 5672:5672
          - 15672:15672
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore
      
    - name: Run All Tests (including integration)
      run: dotnet test --no-build --verbosity normal
      env:
        RUN_INTEGRATION_TESTS: true
        DATABASE_URL: postgresql://conduit_test:conduit_test@localhost:5432/conduit_test
        ConnectionStrings__DefaultConnection: Host=localhost;Database=conduit_test;Username=conduit_test;Password=conduit_test
        ConnectionStrings__Redis: localhost:6379
        CONDUITLLM__RABBITMQ__HOST: localhost
        CONDUITLLM__RABBITMQ__USERNAME: conduit_test
        CONDUITLLM__RABBITMQ__PASSWORD: conduit_test
```

## Matrix Strategy for Multiple Environments

Test against multiple configurations:

```yaml
name: Integration Test Matrix

on:
  workflow_dispatch:

jobs:
  test-matrix:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]
        postgres-version: ['15', '16']
        include:
          - os: ubuntu-latest
            postgres-version: '16'
            run-integration: true
            
    runs-on: ${{ matrix.os }}
    
    services:
      postgres:
        image: postgres:${{ matrix.postgres-version }}-alpine
        # ... (same configuration as above)
        
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
        
    - name: Test
      run: dotnet test
      env:
        RUN_INTEGRATION_TESTS: ${{ matrix.run-integration || 'false' }}
```

## Conditional Integration Tests Based on Changes

Only run integration tests when relevant files change:

```yaml
name: Smart Integration Tests

on:
  pull_request:
    paths:
      - 'ConduitLLM.Core/**'
      - 'ConduitLLM.Http/**'
      - 'ConduitLLM.Admin/**'
      - 'ConduitLLM.IntegrationTests/**'
      - '**/appsettings*.json'

jobs:
  check-changes:
    runs-on: ubuntu-latest
    outputs:
      run-integration: ${{ steps.filter.outputs.integration }}
      
    steps:
    - uses: dorny/paths-filter@v2
      id: filter
      with:
        filters: |
          integration:
            - 'ConduitLLM.Core/Services/**'
            - 'ConduitLLM.Http/Controllers/**'
            - 'ConduitLLM.IntegrationTests/**'
            
  test:
    needs: check-changes
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x
        
    - name: Test
      run: dotnet test
      env:
        RUN_INTEGRATION_TESTS: ${{ needs.check-changes.outputs.run-integration }}
```

## Environment Variables

The integration test framework checks these environment variables:

1. **RUN_INTEGRATION_TESTS**: Set to `true` or `1` to enable integration tests
2. **CI**: If set to `true`, integration tests are disabled unless explicitly enabled
3. **GITHUB_ACTIONS**: Automatically set by GitHub Actions
4. **TF_BUILD**: Set by Azure DevOps

## Best Practices

1. **Default to skipping**: Integration tests should be opt-in for CI builds
2. **Nightly runs**: Schedule full integration test runs during off-peak hours
3. **PR validation**: Run integration tests for PRs that modify core functionality
4. **Service health checks**: Always configure health checks for service containers
5. **Timeouts**: Set appropriate timeouts for integration test jobs
6. **Artifacts**: Collect logs and test results as artifacts for debugging

## Troubleshooting

If integration tests are unexpectedly running or skipping:

1. Check the job logs for environment variable values
2. Verify Docker/services are properly configured
3. Look for the skip message in test output
4. Ensure the `IntegrationFact` attribute is used instead of `Fact`

## Local Development

Developers can control integration tests locally:

```bash
# Skip integration tests (default if Docker not available)
dotnet test

# Run integration tests
RUN_INTEGRATION_TESTS=true dotnet test

# Force skip even with Docker
RUN_INTEGRATION_TESTS=false dotnet test
```