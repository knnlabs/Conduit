#!/bin/bash

# Script to compare the legacy and repository pattern implementations
# This script runs a series of operations against both implementations 
# and compares their results and performance

set -e  # Exit on any error

LOG_DIR="comparison-logs"
LEGACY_LOG="${LOG_DIR}/legacy-$(date +%Y%m%d_%H%M%S).log"
REPO_LOG="${LOG_DIR}/repository-$(date +%Y%m%d_%H%M%S).log"
SUMMARY_LOG="${LOG_DIR}/summary-$(date +%Y%m%d_%H%M%S).log"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Create log directory
mkdir -p $LOG_DIR

log() {
    echo -e "${BLUE}[$(date +"%Y-%m-%d %H:%M:%S")] $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] $1" >> $SUMMARY_LOG
}

success() {
    echo -e "${GREEN}[$(date +"%Y-%m-%d %H:%M:%S")] $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] SUCCESS: $1" >> $SUMMARY_LOG
}

error() {
    echo -e "${RED}[$(date +"%Y-%m-%d %H:%M:%S")] ERROR: $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] ERROR: $1" >> $SUMMARY_LOG
}

warn() {
    echo -e "${YELLOW}[$(date +"%Y-%m-%d %H:%M:%S")] WARNING: $1${NC}"
    echo "[$(date +"%Y-%m-%d %H:%M:%S")] WARNING: $1" >> $SUMMARY_LOG
}

display_help() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -h, --help                 Display this help message"
    echo "  -t, --test [TEST_NAME]     Run a specific test (virtual-keys, logs, cost-dashboard, all)"
    echo ""
    echo "Example:"
    echo "  $0 --test virtual-keys"
    exit 0
}

# Parse command line arguments
TEST_NAME="all"
while [[ $# -gt 0 ]]; do
    key="$1"
    case $key in
        -h|--help)
            display_help
            ;;
        -t|--test)
            TEST_NAME="$2"
            shift
            shift
            ;;
        *)
            error "Unknown option $1"
            display_help
            ;;
    esac
done

log "Starting comparison between legacy and repository pattern implementations"
log "Test: $TEST_NAME"

# Function to run a test case on a specific implementation
run_test_case() {
    local implementation=$1
    local test_name=$2
    local log_file=$3
    
    log "Running $test_name test on $implementation implementation..."
    
    # Set up the environment variables based on implementation
    if [ "$implementation" == "legacy" ]; then
        export CONDUIT_USE_REPOSITORY_PATTERN=false
    else
        export CONDUIT_USE_REPOSITORY_PATTERN=true
    fi
    
    # Build and run with the current implementation
    dotnet build > /dev/null || { error "Build failed for $implementation"; return 1; }
    
    # Start application in background
    dotnet run --project ConduitLLM.WebUI --no-build &
    APP_PID=$!
    
    # Give it some time to start
    sleep 5
    
    # Check if the process is still running
    if ! ps -p $APP_PID > /dev/null; then
        error "Application failed to start for $implementation"
        return 1
    fi
    
    # Run the appropriate test case
    case $test_name in
        virtual-keys)
            test_virtual_keys $implementation $log_file
            ;;
        logs)
            test_logs $implementation $log_file
            ;;
        cost-dashboard)
            test_cost_dashboard $implementation $log_file
            ;;
        all)
            test_virtual_keys $implementation $log_file
            test_logs $implementation $log_file
            test_cost_dashboard $implementation $log_file
            ;;
        *)
            error "Unknown test: $test_name"
            ;;
    esac
    
    # Stop the application
    kill $APP_PID
    wait $APP_PID 2>/dev/null || true
    
    log "Completed $test_name test on $implementation implementation"
}

# Function to test virtual keys functionality
test_virtual_keys() {
    local implementation=$1
    local log_file=$2
    
    log "Testing virtual keys on $implementation..."
    
    # Generate a test master key
    local master_key=$(openssl rand -hex 16)
    export CONDUIT_MASTER_KEY=$master_key
    
    # Start timing
    local start_time=$(date +%s.%N)
    
    # Create a virtual key
    local create_response=$(curl -s -X POST -H "Content-Type: application/json" \
         -H "Authorization: Bearer $master_key" \
         -d '{"keyName":"Test'$implementation'","allowedModels":"gpt-4","maxBudget":100,"budgetDuration":"monthly"}' \
         http://localhost:5000/api/virtualkeys)
    
    echo "$create_response" >> $log_file
    
    # Extract the key ID from the response
    local key_id=$(echo "$create_response" | grep -o '"id":[0-9]*' | grep -o '[0-9]*')
    
    if [ -z "$key_id" ]; then
        error "Failed to create virtual key for $implementation"
        return 1
    fi
    
    # List all keys
    curl -s -H "Authorization: Bearer $master_key" \
         http://localhost:5000/api/virtualkeys >> $log_file
    
    # Update the key
    curl -s -X PUT -H "Content-Type: application/json" \
         -H "Authorization: Bearer $master_key" \
         -d '{"keyName":"Updated'$implementation'","maxBudget":200}' \
         http://localhost:5000/api/virtualkeys/$key_id >> $log_file
    
    # Get the key
    curl -s -H "Authorization: Bearer $master_key" \
         http://localhost:5000/api/virtualkeys/$key_id >> $log_file
    
    # Reset spend
    curl -s -X POST -H "Authorization: Bearer $master_key" \
         http://localhost:5000/api/virtualkeys/$key_id/reset-spend >> $log_file
    
    # Delete the key
    curl -s -X DELETE -H "Authorization: Bearer $master_key" \
         http://localhost:5000/api/virtualkeys/$key_id >> $log_file
    
    # End timing
    local end_time=$(date +%s.%N)
    local duration=$(echo "$end_time - $start_time" | bc)
    
    success "Virtual keys test completed for $implementation in $duration seconds"
}

# Function to test logs functionality
test_logs() {
    local implementation=$1
    local log_file=$2
    
    log "Testing logs on $implementation..."
    
    # Generate a test master key
    local master_key=$(openssl rand -hex 16)
    export CONDUIT_MASTER_KEY=$master_key
    
    # Start timing
    local start_time=$(date +%s.%N)
    
    # Search logs
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/logs/search?page=1&pageSize=10" >> $log_file
    
    # Get logs summary
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/logs/summary" >> $log_file
    
    # Get virtual keys for filtering
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/logs/keys" >> $log_file
    
    # Get models for filtering
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/logs/models" >> $log_file
    
    # End timing
    local end_time=$(date +%s.%N)
    local duration=$(echo "$end_time - $start_time" | bc)
    
    success "Logs test completed for $implementation in $duration seconds"
}

# Function to test cost dashboard functionality
test_cost_dashboard() {
    local implementation=$1
    local log_file=$2
    
    log "Testing cost dashboard on $implementation..."
    
    # Generate a test master key
    local master_key=$(openssl rand -hex 16)
    export CONDUIT_MASTER_KEY=$master_key
    
    # Start timing
    local start_time=$(date +%s.%N)
    
    # Get cost dashboard data
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/costdashboard" >> $log_file
    
    # Get cost by model
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/costdashboard/by-model" >> $log_file
    
    # Get cost by virtual key
    curl -s -H "Authorization: Bearer $master_key" \
         "http://localhost:5000/api/costdashboard/by-key" >> $log_file
    
    # End timing
    local end_time=$(date +%s.%N)
    local duration=$(echo "$end_time - $start_time" | bc)
    
    success "Cost dashboard test completed for $implementation in $duration seconds"
}

# Run the tests with both implementations
log "Running tests with legacy implementation..."
run_test_case "legacy" "$TEST_NAME" "$LEGACY_LOG"
legacy_exit_code=$?

log "Running tests with repository implementation..."
run_test_case "repository" "$TEST_NAME" "$REPO_LOG"
repo_exit_code=$?

# Compare results
log "Comparing results..."

# Check if both implementations completed successfully
if [ $legacy_exit_code -eq 0 ] && [ $repo_exit_code -eq 0 ]; then
    success "Both implementations completed successfully"
    
    # Compare log files to check for any differences
    if diff -q "$LEGACY_LOG" "$REPO_LOG" >/dev/null; then
        success "Test results are identical for both implementations"
    else
        warn "Test results differ between implementations"
        echo "Differences:" >> $SUMMARY_LOG
        diff "$LEGACY_LOG" "$REPO_LOG" >> $SUMMARY_LOG 2>&1
    fi
else
    error "At least one implementation failed"
    [ $legacy_exit_code -ne 0 ] && error "Legacy implementation failed"
    [ $repo_exit_code -ne 0 ] && error "Repository implementation failed"
fi

log "Comparison completed. Check $SUMMARY_LOG for details."