#!/bin/bash
# Run all tests in the solution
# Integration tests have been moved to archive

set -euo pipefail

# Colors for output
if [ -t 1 ]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    BLUE='\033[0;34m'
    NC='\033[0m'
else
    RED=''
    GREEN=''
    YELLOW=''
    BLUE=''
    NC=''
fi

echo -e "${BLUE}==>${NC} Running all tests..."
echo ""

# Optional: Accept test filter as argument
TEST_FILTER="${1:-}"

# Simple dotnet test command now that integration tests are gone
TEST_CMD="dotnet test"

# Add configuration
TEST_CMD="$TEST_CMD --configuration Debug"

# Add verbosity for better output
TEST_CMD="$TEST_CMD --logger \"console;verbosity=normal\""

# Add test filter if provided
if [ -n "$TEST_FILTER" ]; then
    TEST_CMD="$TEST_CMD --filter \"$TEST_FILTER\""
    echo -e "${BLUE}Using test filter:${NC} $TEST_FILTER"
fi

# Run tests
echo -e "${BLUE}Running:${NC} $TEST_CMD"
echo ""

if $TEST_CMD; then
    echo ""
    echo -e "${GREEN}✓ All tests passed!${NC}"
else
    echo ""
    echo -e "${RED}✗ Some tests failed${NC}"
    exit 1
fi