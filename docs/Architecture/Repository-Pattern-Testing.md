# Repository Pattern Testing Guide

This document outlines the process for testing the repository pattern implementation in a staging environment before enabling it in production.

## Overview

The migration from direct database access to the repository pattern is a significant architectural change. Testing in a staging environment helps ensure that the implementation works correctly and doesn't cause any regressions.

## Prerequisites

- A staging environment with a separate database from production
- Access to staging environment deployment tools
- The `test-repository-pattern.sh` script

## Testing Process

### Step 1: Prepare the Staging Environment

1. Set up a staging environment with a clean database:

```bash
# Option 1: Using SQLite
export CONDUIT_DATABASE_PROVIDER=sqlite
export CONDUIT_DATABASE_CONNECTION_STRING="Data Source=staging.db"

# Option 2: Using PostgreSQL
export CONDUIT_DATABASE_PROVIDER=postgres
export CONDUIT_DATABASE_CONNECTION_STRING="Host=localhost;Database=conduit_staging;Username=postgres;Password=your_password"
```

2. Set the following environment variables to enable the repository pattern:

```bash
export CONDUIT_USE_REPOSITORY_PATTERN=true
export ASPNETCORE_ENVIRONMENT=Staging
```

### Step 2: Run the Testing Script

The testing script automates the process of building, deploying, and testing the application with the repository pattern enabled.

```bash
./test-repository-pattern.sh --connection "Your connection string here"
```

The script will:
- Build the application with the repository pattern enabled
- Start the WebUI application
- Run a series of tests against the API endpoints
- Report success or failure

### Step 3: Manual Validation

After the automated tests, perform these manual validation checks:

1. **Virtual Keys Management**:
   - Create a new virtual key
   - List all virtual keys
   - Update a virtual key
   - Delete a virtual key

2. **Request Logs**:
   - Make several API requests with different virtual keys
   - View the request logs in the dashboard
   - Verify that the logs include all the expected information
   - Check filtering and pagination functionality

3. **Cost Dashboard**:
   - Verify that the cost dashboard displays correct information
   - Check that costs are properly associated with virtual keys
   - Ensure historical data is displayed correctly

4. **Router Configuration**:
   - Update router configuration settings
   - Verify that the changes are correctly persisted
   - Test that the router respects the new configuration

### Step 4: Performance Testing

Measure and compare performance metrics between the old and new implementations:

1. **Database Query Performance**:
   - Response time for key database operations
   - Number of database queries per operation
   - Connection pool usage

2. **API Response Times**:
   - Response time for key API endpoints
   - Throughput under load

3. **Memory Usage**:
   - Monitor memory usage during operation
   - Check for any memory leaks over time

### Step 5: Rollback Plan

In case issues are found, have a rollback plan ready:

1. Set `CONDUIT_USE_REPOSITORY_PATTERN=false` to switch back to the old implementation
2. Restart the application
3. Verify that the system functions correctly with the old implementation

## Testing Results

Document testing results including:

1. Automated test results
2. Manual validation findings
3. Performance comparison metrics
4. Any issues discovered and their resolutions

## Next Steps After Successful Testing

If all tests pass and performance is acceptable, proceed with:

1. Updating documentation with the new architecture
2. Scheduling a gradual rollout to production environments
3. Planning for the eventual removal of the legacy implementations

## Troubleshooting Common Issues

### Database Migration Issues

If you encounter database migration issues:

```bash
# Check migration status
dotnet ef migrations list --project ConduitLLM.Configuration

# Manually apply migrations if needed
dotnet ef database update --project ConduitLLM.Configuration
```

### Repository Service Registration Issues

If services are not correctly registered:

1. Check `Program.cs` for correct service registration
2. Verify that the `AddRepositoryServices()` extension method is called
3. Ensure that the `CONDUIT_USE_REPOSITORY_PATTERN` environment variable is correctly set

### Controller Mapping Issues

If controller endpoints are not responding:

1. Check the route configuration in the respective controller files
2. Verify that the new controllers with "New" suffix are correctly registered
3. Check for any authorization issues in the new controllers