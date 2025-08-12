#!/bin/bash

# Integration Test Runner for Conduit
# This script runs integration tests for all or specific providers
# Usage: ./tests-integration.sh [provider]
# Examples:
#   ./tests-integration.sh           # Run all providers
#   ./tests-integration.sh groq      # Run only Groq tests
#   ./tests-integration.sh sambanova # Run only SambaNova tests

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color
BOLD='\033[1m'

# Provider configuration
PROVIDER_ARG="${1:-all}"
SUPPORTED_PROVIDERS=("groq" "sambanova")

# Function to show usage
show_usage() {
    echo "Usage: $0 [provider]"
    echo
    echo "Providers:"
    echo "  all        - Run tests for all providers (default)"
    for provider in "${SUPPORTED_PROVIDERS[@]}"; do
        echo "  $provider  - Run tests for $provider only"
    done
    echo
}

# Handle help argument
if [ "$PROVIDER_ARG" = "--help" ] || [ "$PROVIDER_ARG" = "-h" ]; then
    show_usage
    exit 0
fi

# Validate provider argument
if [ "$PROVIDER_ARG" != "all" ] && [[ ! " ${SUPPORTED_PROVIDERS[@]} " =~ " ${PROVIDER_ARG} " ]]; then
    echo -e "${RED}❌ Invalid provider: $PROVIDER_ARG${NC}"
    echo
    show_usage
    exit 1
fi

# Get the script directory and navigate to root of Conduit
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"
TEST_DIR="$ROOT_DIR/ConduitLLM.IntegrationTests"

if [ "$PROVIDER_ARG" = "all" ]; then
    echo -e "${BLUE}${BOLD}==================================${NC}"
    echo -e "${BLUE}${BOLD}  Conduit Integration Test Runner ${NC}"
    echo -e "${BLUE}${BOLD}  Running ALL providers            ${NC}"
    echo -e "${BLUE}${BOLD}==================================${NC}"
else
    echo -e "${BLUE}${BOLD}========================================${NC}"
    echo -e "${BLUE}${BOLD}  $(echo "$PROVIDER_ARG" | tr '[:lower:]' '[:upper:]') Integration Test Runner${NC}"
    echo -e "${BLUE}${BOLD}========================================${NC}"
fi
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

# Function to check and setup provider config
check_provider_config() {
    local provider="$1"
    local provider_config="Config/providers/${provider}.yaml"
    local template_config="Config/providers/${provider}.template.yaml"
    local config_created=false
    
    if [ ! -f "$provider_config" ]; then
        echo -e "${YELLOW}⚠️  Provider config not found: $provider_config${NC}"
        echo -e "${YELLOW}   Creating from template...${NC}"
        
        if [ -f "$template_config" ]; then
            cp "$template_config" "$provider_config"
            echo -e "${GREEN}✓ Created $provider_config${NC}"
            config_created=true
            
            case "$provider" in
                "groq")
                    echo -e "${BLUE}   📝 Opening in VS Code for you to add your Groq API key...${NC}"
                    echo -e "${YELLOW}   Look for: apiKey: \"gsk_YOUR_GROQ_API_KEY_HERE\"${NC}"
                    ;;
                "sambanova")
                    echo -e "${BLUE}   📝 Opening in VS Code for you to add your SambaNova API key...${NC}"
                    echo -e "${YELLOW}   Look for: apiKey: \"YOUR_SAMBANOVA_API_KEY_HERE\"${NC}"
                    ;;
            esac
            
            # Open in VS Code
            if command -v code >/dev/null 2>&1; then
                code "$TEST_DIR/$provider_config"
                echo -e "${GREEN}   ✓ Opened in VS Code${NC}"
            else
                echo -e "${YELLOW}   ⚠️  VS Code not found. Please edit manually: $TEST_DIR/$provider_config${NC}"
            fi
            
            echo
            echo -e "${BLUE}${BOLD}Please configure your API key and run the script again.${NC}"
            exit 1
        else
            echo -e "${RED}❌ Template file not found: $template_config${NC}"
            exit 1
        fi
    fi
    
    # Check if API key is configured
    case "$provider" in
        "groq")
            if grep -q "gsk_YOUR_GROQ_API_KEY_HERE" "$provider_config" 2>/dev/null; then
                echo -e "${RED}❌ Groq API key not configured!${NC}"
                echo -e "${BLUE}   Opening in VS Code for editing...${NC}"
                if command -v code >/dev/null 2>&1; then
                    code "$TEST_DIR/$provider_config"
                    echo -e "${GREEN}   ✓ Opened in VS Code${NC}"
                else
                    echo -e "${YELLOW}   Edit manually: $TEST_DIR/$provider_config${NC}"
                fi
                echo -e "${YELLOW}   Replace: gsk_YOUR_GROQ_API_KEY_HERE with your actual API key${NC}"
                exit 1
            fi
            ;;
        "sambanova")
            if grep -q "YOUR_SAMBANOVA_API_KEY_HERE" "$provider_config" 2>/dev/null; then
                echo -e "${RED}❌ SambaNova API key not configured!${NC}"
                echo -e "${BLUE}   Opening in VS Code for editing...${NC}"
                if command -v code >/dev/null 2>&1; then
                    code "$TEST_DIR/$provider_config"
                    echo -e "${GREEN}   ✓ Opened in VS Code${NC}"
                else
                    echo -e "${YELLOW}   Edit manually: $TEST_DIR/$provider_config${NC}"
                fi
                echo -e "${YELLOW}   Replace: YOUR_SAMBANOVA_API_KEY_HERE with your actual API key${NC}"
                exit 1
            fi
            ;;
    esac
    
    echo -e "${GREEN}✓ $provider configuration verified${NC}"
}

# Check base config files
CONFIG_FILE="Config/test-config.yaml"

if [ ! -f "$CONFIG_FILE" ]; then
    echo -e "${YELLOW}⚠️  Configuration file not found: $CONFIG_FILE${NC}"
    echo -e "${YELLOW}   Creating from template...${NC}"
    
    if [ -f "Config/test-config.template.yaml" ]; then
        cp Config/test-config.template.yaml "$CONFIG_FILE"
        echo -e "${GREEN}✓ Created $CONFIG_FILE${NC}"
        echo -e "${BLUE}   📝 Opening in VS Code for you to configure your Admin API key...${NC}"
        echo -e "${YELLOW}   Currently set to 'alpha' - replace with your actual Admin API key${NC}"
        
        # Open in VS Code
        if command -v code >/dev/null 2>&1; then
            code "$TEST_DIR/$CONFIG_FILE"
            echo -e "${GREEN}   ✓ Opened in VS Code${NC}"
        else
            echo -e "${YELLOW}   ⚠️  VS Code not found. Please edit manually: $TEST_DIR/$CONFIG_FILE${NC}"
        fi
        
        echo
        echo -e "${BLUE}${BOLD}Please configure your Admin API key and run the script again.${NC}"
        exit 1
    else
        echo -e "${RED}❌ Template file not found: Config/test-config.template.yaml${NC}"
        exit 1
    fi
fi

# Check provider configurations based on what we're running
if [ "$PROVIDER_ARG" = "all" ]; then
    for provider in "${SUPPORTED_PROVIDERS[@]}"; do
        check_provider_config "$provider"
    done
else
    check_provider_config "$PROVIDER_ARG"
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

# Function to run tests for a specific provider
run_provider_tests() {
    local provider="$1"
    echo -e "${BLUE}🧪 Running $provider integration tests...${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    case "$provider" in
        "groq")
            dotnet test --no-build --logger "console;verbosity=normal"
            ;;
        "sambanova")
            dotnet test --no-build --filter "FullyQualifiedName~SambaNovaEndToEndTest" --logger "console;verbosity=normal"
            ;;
        *)
            echo -e "${RED}❌ Unknown provider test configuration: $provider${NC}"
            return 1
            ;;
    esac
}

# Run tests based on provider argument
set +e  # Don't exit on test failure
TEST_EXIT_CODE=0

if [ "$PROVIDER_ARG" = "all" ]; then
    echo -e "${BLUE}🧪 Running integration tests for ALL providers...${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    # Run all provider tests
    dotnet test --no-build --logger "console;verbosity=normal"
    TEST_EXIT_CODE=$?
else
    # Run specific provider tests
    run_provider_tests "$PROVIDER_ARG"
    TEST_EXIT_CODE=$?
fi

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
    if [ "$PROVIDER_ARG" = "all" ]; then
        echo -e "${GREEN}${BOLD}✅ All tests passed!${NC}"
    else
        echo -e "${GREEN}${BOLD}✅ All $PROVIDER_ARG tests passed!${NC}"
        if [ "$PROVIDER_ARG" = "sambanova" ]; then
            echo -e "${BLUE}Note: If multimodal tests were skipped, the model may not support image inputs.${NC}"
        fi
    fi
else
    if [ "$PROVIDER_ARG" = "all" ]; then
        echo -e "${RED}${BOLD}❌ Some tests failed. Check the report for details.${NC}"
    else
        echo -e "${RED}${BOLD}❌ Some $PROVIDER_ARG tests failed. Check the report for details.${NC}"
    fi
fi

exit $TEST_EXIT_CODE