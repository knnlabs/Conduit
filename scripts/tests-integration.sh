#!/bin/bash

# Integration Test Runner for Conduit
# This script runs the integration tests and displays the report location

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color
BOLD='\033[1m'

# Get the script directory and navigate to root of Conduit
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"
TEST_DIR="$ROOT_DIR/ConduitLLM.IntegrationTests"

echo -e "${BLUE}${BOLD}==================================${NC}"
echo -e "${BLUE}${BOLD}  Conduit Integration Test Runner ${NC}"
echo -e "${BLUE}${BOLD}==================================${NC}"
echo

# Check if test directory exists
if [ ! -d "$TEST_DIR" ]; then
    echo -e "${RED}❌ Integration test directory not found: $TEST_DIR${NC}"
    exit 1
fi

# Navigate to test directory
cd "$TEST_DIR"
echo -e "${GREEN}📁 Working directory: $(pwd)${NC}"
echo

# Check if config files exist
CONFIG_FILE="Config/test-config.yaml"
PROVIDER_CONFIG="Config/providers/groq.yaml"

if [ ! -f "$CONFIG_FILE" ]; then
    echo -e "${YELLOW}⚠️  Configuration file not found: $CONFIG_FILE${NC}"
    echo -e "${YELLOW}   Creating from template...${NC}"
    
    if [ -f "Config/test-config.template.yaml" ]; then
        cp Config/test-config.template.yaml "$CONFIG_FILE"
        echo -e "${GREEN}✓ Created $CONFIG_FILE${NC}"
        echo -e "${YELLOW}   Please edit it with your Admin API key (currently set to 'alpha')${NC}"
    else
        echo -e "${RED}❌ Template file not found: Config/test-config.template.yaml${NC}"
        exit 1
    fi
fi

if [ ! -f "$PROVIDER_CONFIG" ]; then
    echo -e "${YELLOW}⚠️  Provider config not found: $PROVIDER_CONFIG${NC}"
    echo -e "${YELLOW}   Creating from template...${NC}"
    
    if [ -f "Config/providers/groq.template.yaml" ]; then
        cp Config/providers/groq.template.yaml "$PROVIDER_CONFIG"
        echo -e "${GREEN}✓ Created $PROVIDER_CONFIG${NC}"
        echo -e "${RED}   ⚠️  You MUST edit this file and add your Groq API key!${NC}"
        echo -e "${RED}   Edit: $TEST_DIR/$PROVIDER_CONFIG${NC}"
        echo -e "${RED}   Look for: apiKey: \"gsk_YOUR_GROQ_API_KEY_HERE\"${NC}"
        exit 1
    else
        echo -e "${RED}❌ Template file not found: Config/providers/groq.template.yaml${NC}"
        exit 1
    fi
fi

# Check if API key is configured
if grep -q "gsk_YOUR_GROQ_API_KEY_HERE" "$PROVIDER_CONFIG" 2>/dev/null; then
    echo -e "${RED}❌ Groq API key not configured!${NC}"
    echo -e "${RED}   Edit: $TEST_DIR/$PROVIDER_CONFIG${NC}"
    echo -e "${RED}   Replace: gsk_YOUR_GROQ_API_KEY_HERE with your actual API key${NC}"
    exit 1
fi

# Check if services are running
echo -e "${BLUE}🔍 Checking if services are running...${NC}"

# Check Core API
if curl -s -f http://localhost:5000/health > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Core API is running${NC}"
else
    echo -e "${RED}❌ Core API is not running${NC}"
    echo -e "${YELLOW}   Start services with: ./scripts/start-dev.sh${NC}"
    exit 1
fi

# Check Admin API
if curl -s -f http://localhost:5002/health > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Admin API is running${NC}"
else
    echo -e "${RED}❌ Admin API is not running${NC}"
    echo -e "${YELLOW}   Start services with: ./scripts/start-dev.sh${NC}"
    exit 1
fi

echo

# Build the test project
echo -e "${BLUE}🔨 Building test project...${NC}"
if dotnet build --nologo --verbosity quiet; then
    echo -e "${GREEN}✓ Build successful${NC}"
else
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi

echo

# Run the tests
echo -e "${BLUE}🧪 Running integration tests...${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Run tests and capture exit code
set +e  # Don't exit on test failure
dotnet test --no-build --logger "console;verbosity=normal"
TEST_EXIT_CODE=$?
set -e

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo

# Find the most recent report
# Reports are generated in the bin/Debug/net9.0/Reports directory
REPORT_DIR="$TEST_DIR/bin/Debug/net9.0/Reports"
if [ -d "$REPORT_DIR" ]; then
    LATEST_REPORT=$(ls -t "$REPORT_DIR"/*.md 2>/dev/null | head -n1)
    
    if [ -n "$LATEST_REPORT" ]; then
        echo -e "${GREEN}${BOLD}📄 Test Report Generated:${NC}"
        echo -e "${YELLOW}   $LATEST_REPORT${NC}"
        echo
        
        # Display report summary
        echo -e "${BLUE}${BOLD}Report Summary:${NC}"
        echo -e "${BLUE}───────────────${NC}"
        
        # Extract summary section from report
        if [ -f "$LATEST_REPORT" ]; then
            # Look for the Summary section and display it
            awk '/^## Summary/,/^##/{if(/^##/ && !/^## Summary/) exit; print}' "$LATEST_REPORT" | tail -n +2
        fi
        
        echo
        echo -e "${GREEN}${BOLD}View full report:${NC}"
        echo -e "  ${YELLOW}cat $LATEST_REPORT${NC}"
        echo -e "  ${YELLOW}code $LATEST_REPORT${NC}  # Open in VS Code"
        echo -e "  ${YELLOW}less $LATEST_REPORT${NC}  # View in terminal"
    else
        echo -e "${YELLOW}⚠️  No test report found in $REPORT_DIR${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  Report directory not found: $REPORT_DIR${NC}"
fi

echo

# Check test context for debugging
CONTEXT_FILE="$TEST_DIR/bin/Debug/net9.0/test-context.json"
if [ -f "$CONTEXT_FILE" ]; then
    echo -e "${BLUE}🔍 Test context saved for debugging:${NC}"
    echo -e "   ${YELLOW}$CONTEXT_FILE${NC}"
    echo
fi

# Exit with test exit code
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✅ All tests passed!${NC}"
else
    echo -e "${RED}${BOLD}❌ Some tests failed. Check the report for details.${NC}"
fi

exit $TEST_EXIT_CODE