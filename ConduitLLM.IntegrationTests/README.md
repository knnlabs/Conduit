# ConduitLLM Integration Tests

End-to-end integration tests for Conduit that verify the complete flow from provider setup through virtual key billing.

## Overview

These integration tests verify the complete functionality of Conduit by:
1. Creating a provider with API keys
2. Setting up model mappings and costs
3. Creating virtual key groups with credit
4. Sending chat requests through virtual keys
5. Verifying token tracking and billing accuracy

## Prerequisites

1. **Docker Environment Running**: Start the development environment:
   ```bash
   ./scripts/start-dev.sh
   ```

2. **Services Health**: The tests will automatically wait for all services to be healthy:
   - Core API (http://localhost:5000)
   - Admin API (http://localhost:5002)
   - PostgreSQL
   - Redis
   - RabbitMQ (if configured)

## Configuration

### Step 1: Copy Configuration Templates

```bash
cd ConduitLLM.IntegrationTests

# Copy main configuration
cp Config/test-config.template.yaml Config/test-config.yaml

# Copy provider configuration
cp Config/providers/groq.template.yaml Config/providers/groq.yaml
```

### Step 2: Configure test-config.yaml

Edit `Config/test-config.yaml` and set:
- `adminApiKey`: Get from `docker-compose.dev.yml` (CONDUIT_API_TO_API_BACKEND_AUTH_KEY)
- Leave other settings as default unless you have custom configurations

### Step 3: Configure Provider API Keys

Edit `Config/providers/groq.yaml` and add your Groq API key:
```yaml
provider:
  apiKey: "gsk_YOUR_ACTUAL_GROQ_API_KEY_HERE"
```

## Running Tests

### Run All Tests
```bash
cd ConduitLLM.IntegrationTests
dotnet test
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test Step
```bash
dotnet test --filter "FullyQualifiedName~Step01"
```

## Test Flow

The test executes the following steps in order:

1. **Step01_CreateProvider**: Creates a TEST_ prefixed provider
2. **Step02_CreateProviderKey**: Adds API key as default and primary
3. **Step03_CreateModelMapping**: Maps TEST_gemma2-9b to gemma2-9b-it
4. **Step04_CreateModelCost**: Sets pricing at $0.20 per million tokens
5. **Step05_CreateVirtualKeyGroup**: Creates group with $100 credit
6. **Step06_CreateVirtualKey**: Generates virtual key for API access
7. **Step07_SendChatRequest**: Sends "What is the history of France?"
8. **Step08_VerifyTokenTracking**: Validates token counts match provider response
9. **Step09_VerifyVirtualKeyDebit**: Confirms accurate billing (micro-cents precision)
10. **Step10_GenerateReport**: Creates markdown report in Reports/ directory

## Test Reports

After each test run, a detailed markdown report is generated:
- Location: `Reports/test_run_{timestamp}.md`
- Contents:
  - Provider setup details
  - Token usage and costs
  - Response validation results
  - Any errors encountered

## Debugging Failed Tests

### Test Context
The test saves its state to `test-context.json` after each step. This file contains:
- All created entity IDs
- Last chat response details
- Cost calculations
- Error messages

### Manual Database Inspection
All test entities are prefixed with `TEST_` for easy identification:
```sql
-- View test providers
SELECT * FROM "Providers" WHERE "Name" LIKE 'TEST_%';

-- View test virtual keys
SELECT * FROM "VirtualKeys" WHERE "Name" LIKE 'TEST_%';

-- View test transactions
SELECT * FROM "VirtualKeySpendHistory" 
WHERE "VirtualKey" IN (
  SELECT "VirtualKey" FROM "VirtualKeys" WHERE "Name" LIKE 'TEST_%'
);
```

## Expanding Tests

### Adding New Providers

1. Create provider configuration:
   ```bash
   cp Config/providers/groq.template.yaml Config/providers/openai.template.yaml
   # Edit to match OpenAI specifics
   ```

2. Add to active providers in `test-config.yaml`:
   ```yaml
   activeProviders:
     - groq
     - openai
   ```

3. Future: The test framework is designed to support:
   - All provider types (OpenAI, Anthropic, Cerebras, etc.)
   - Multimodal inputs (images with chat)
   - Image generation testing
   - Video generation testing

### Test Data Persistence

Tests intentionally DO NOT clean up data to allow:
- Manual verification of results
- Debugging of failures
- Audit trail of test runs

To clean test data manually:
```sql
DELETE FROM "VirtualKeySpendHistory" WHERE "VirtualKey" IN (
  SELECT "VirtualKey" FROM "VirtualKeys" WHERE "Name" LIKE 'TEST_%'
);
DELETE FROM "VirtualKeys" WHERE "Name" LIKE 'TEST_%';
DELETE FROM "VirtualKeyGroups" WHERE "Name" LIKE 'TEST_%';
DELETE FROM "ModelCosts" WHERE "ModelPattern" LIKE 'TEST_%';
DELETE FROM "ModelProviderMappings" WHERE "ModelAlias" LIKE 'TEST_%';
DELETE FROM "ProviderKeyCredentials" WHERE "ProviderId" IN (
  SELECT "Id" FROM "Providers" WHERE "Name" LIKE 'TEST_%'
);
DELETE FROM "Providers" WHERE "Name" LIKE 'TEST_%';
```

## Common Issues

### Build Errors
- Ensure you're using .NET 9.0 SDK
- Run `dotnet restore` if package errors occur

### Configuration Not Found
- Ensure you copied the template files
- Check file names match exactly (case-sensitive)

### API Authentication Fails
- Verify `adminApiKey` matches `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` in docker-compose.dev.yml
- Ensure services are running with `docker ps`

### Provider API Errors
- Verify your API key is valid and has credits
- Check provider service status
- Review rate limits for your API key tier

### Test Timeouts
- Default chat timeout is 60 seconds
- Adjust in `test-config.yaml` if needed for slower providers

## Cost Tracking

Each test run uses minimal API credits (typically < $0.01):
- Groq gemma2-9b: ~$0.000051 per test
- Costs are tracked with micro-cent precision (6 decimal places)
- Virtual key starts with $100 credit (configurable)

## TODO: Future Enhancements

- [ ] Parallel provider testing
- [ ] Multimodal input testing (images)
- [ ] Image generation verification
- [ ] Video generation verification
- [ ] Streaming response validation
- [ ] Rate limiting tests
- [ ] Error handling scenarios
- [ ] Performance benchmarking
- [ ] Load testing capabilities
- [ ] CI/CD integration