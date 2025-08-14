#!/bin/bash

# Integration Test Runner for Conduit
# Runs provider billing integration tests
# Usage: ./tests-integration.sh

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"

echo -e "${BLUE}================================${NC}"
echo -e "${BLUE} CONDUIT INTEGRATION TESTS${NC}"
echo -e "${BLUE}================================${NC}"
echo

# Check if services are running
echo -e "${GREEN}Checking services...${NC}"
if ! curl -s -f http://localhost:5000/health > /dev/null 2>&1; then
    echo -e "${RED}✗ Core API is not running${NC}"
    echo -e "  Start services with: ./scripts/start-dev.sh"
    exit 1
fi

if ! curl -s -f http://localhost:5002/health > /dev/null 2>&1; then
    echo -e "${RED}✗ Admin API is not running${NC}"
    echo -e "  Start services with: ./scripts/start-dev.sh"
    exit 1
fi

echo -e "${GREEN}✓ Services are running${NC}"
echo

# Build the test project
echo -e "${GREEN}Building tests...${NC}"
cd "$ROOT_DIR/ConduitLLM.IntegrationTests"
if ! dotnet build --nologo --verbosity quiet; then
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Build successful${NC}"
echo

# Run the tests
echo -e "${BLUE}Running integration tests...${NC}"
echo -e "${BLUE}================================${NC}"

# Run with clean output
dotnet test \
    --no-build \
    --filter "FullyQualifiedName~ProviderBillingIntegrationTests" \
    --logger "console;verbosity=normal" \
    2>&1 | grep -v "^Test run for" | grep -v "^VSTest version" | grep -v "^Starting test" | grep -v "^A total of"

# Check exit code
if [ ${PIPESTATUS[0]} -eq 0 ]; then
    echo
    echo -e "${GREEN}================================${NC}"
    echo -e "${GREEN} ALL TESTS PASSED${NC}"
    echo -e "${GREEN}================================${NC}"
    exit 0
else
    echo
    echo -e "${RED}================================${NC}"
    echo -e "${RED} TESTS FAILED${NC}"
    echo -e "${RED}================================${NC}"
    exit 1
fi