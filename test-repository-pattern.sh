#!/bin/bash

# Script to test the repository pattern implementation in a staging environment
# This script will:
# 1. Build the application with repository pattern enabled
# 2. Run a series of tests to validate functionality
# 3. Report results

set -e  # Exit on any error

# Default values
STAGING_DB_CONNECTION="${STAGING_DB_CONNECTION:-Data Source=staging.db}"
STAGING_ENV="Staging"
LOG_FILE="repository-pattern-test-$(date +%Y%m%d_%H%M%S).log"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date +"%Y-%m-%d %H:%M:%S")] $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] $1" >> $LOG_FILE
}

error() {
    echo -e "${RED}[$(date +"%Y-%m-%d %H:%M:%S")] ERROR: $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] ERROR: $1" >> $LOG_FILE
}

warn() {
    echo -e "${YELLOW}[$(date +"%Y-%m-%d %H:%M:%S")] WARNING: $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] WARNING: $1" >> $LOG_FILE
}

display_help() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -h, --help                 Display this help message"
    echo "  -c, --connection [STRING]  Database connection string for staging"
    echo "  -e, --environment [STRING] Environment name (default: Staging)"
    echo ""
    echo "Example:"
    echo "  $0 --connection \"Data Source=staging.db\""
    exit 0
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    key="$1"
    case $key in
        -h|--help)
            display_help
            ;;
        -c|--connection)
            STAGING_DB_CONNECTION="$2"
            shift
            shift
            ;;
        -e|--environment)
            STAGING_ENV="$2"
            shift
            shift
            ;;
        *)
            error "Unknown option $1"
            display_help
            ;;
    esac
done

log "Starting repository pattern testing in staging environment"
log "Database connection: $STAGING_DB_CONNECTION"
log "Environment: $STAGING_ENV"

# Clean previous builds
log "Cleaning previous builds..."
dotnet clean

# Build with repository pattern enabled
log "Building the application with repository pattern enabled..."
export ASPNETCORE_ENVIRONMENT=$STAGING_ENV
export CONDUIT_USE_REPOSITORY_PATTERN=true
export CONDUIT_DATABASE_CONNECTION_STRING="$STAGING_DB_CONNECTION"

dotnet build || { error "Build failed"; exit 1; }

# Start the application in WebUI mode
log "Starting the application in WebUI mode..."
# Run in background and save PID
dotnet run --project ConduitLLM.WebUI --no-build &
WEBUI_PID=$!

# Give it some time to start
log "Waiting for application to start..."
sleep 5

# Check if the process is still running
if ! ps -p $WEBUI_PID > /dev/null; then
    error "Application failed to start"
    exit 1
fi

log "Application started with PID $WEBUI_PID"

# Execute a series of API tests
log "Running functional validation tests..."

# Function to test an API endpoint
test_endpoint() {
    local endpoint=$1
    local description=$2
    local expected_status=${3:-200}
    
    log "Testing: $description ($endpoint)"
    
    status_code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/$endpoint)
    
    if [ "$status_code" -eq "$expected_status" ]; then
        log "✅ Test passed: $description"
        return 0
    else
        error "❌ Test failed: $description (Expected: $expected_status, Got: $status_code)"
        return 1
    fi
}

# Define the test cases
TEST_CASES=(
    "api/health|Health check endpoint"
    "api/virtualkeys|List virtual keys endpoint|401"  # Unauthorized
)

failures=0
total=0

# Run the test cases
for test_case in "${TEST_CASES[@]}"; do
    IFS='|' read -r endpoint description expected_status <<< "$test_case"
    
    total=$((total + 1))
    
    if ! test_endpoint "$endpoint" "$description" "$expected_status"; then
        failures=$((failures + 1))
    fi
done

# Generate a test master key for further tests
export MASTER_KEY=$(openssl rand -hex 16)
export CONDUIT_MASTER_KEY=$MASTER_KEY

log "Generated test master key: $MASTER_KEY"

# Test creating a virtual key with the repository implementation
log "Testing virtual key creation..."
curl -s -X POST -H "Content-Type: application/json" \
     -H "Authorization: Bearer $MASTER_KEY" \
     -d '{"keyName":"TestKey","allowedModels":"gpt-4","maxBudget":100,"budgetDuration":"monthly"}' \
     http://localhost:5000/api/virtualkeys

# Test virtual key list again with authorization
test_endpoint "api/virtualkeys" "List virtual keys with auth" 200

# Stop the application
log "Stopping the application..."
kill $WEBUI_PID

# Display summary
if [ $failures -eq 0 ]; then
    log "All $total tests passed!"
else
    error "$failures out of $total tests failed."
fi

log "Test results saved to $LOG_FILE"
log "Repository pattern testing completed."

# Set execution status
if [ $failures -eq 0 ]; then
    exit 0
else
    exit 1
fi