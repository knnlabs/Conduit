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

# Clean up old test data first
echo -e "${BLUE}🧹 Cleaning up old test data...${NC}"
"$SCRIPT_DIR/cleanup-test-data.sh" > /dev/null 2>&1
echo -e "${GREEN}✓ Cleanup complete${NC}"

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
            dotnet test --no-build --filter "FullyQualifiedName~GroqEndToEndTest" --logger "console;verbosity=normal"
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

# Create temporary file to capture test output
TEST_OUTPUT_FILE=$(mktemp /tmp/conduit-test-output.XXXXXX)
REPORTS_BEFORE=""
REPORTS_AFTER=""

# Capture initial report files
REPORT_DIR="$TEST_DIR/bin/Debug/net9.0/Reports"
if [ -d "$REPORT_DIR" ]; then
    REPORTS_BEFORE=$(ls -t "$REPORT_DIR"/*.md 2>/dev/null | head -20)
fi

if [ "$PROVIDER_ARG" = "all" ]; then
    echo -e "${BLUE}🧪 Running integration tests for ALL providers...${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    # Run all provider tests and capture output
    dotnet test --no-build --logger "console;verbosity=normal" 2>&1 | tee "$TEST_OUTPUT_FILE"
    TEST_EXIT_CODE=${PIPESTATUS[0]}
else
    # Run specific provider tests
    run_provider_tests "$PROVIDER_ARG" 2>&1 | tee "$TEST_OUTPUT_FILE"
    TEST_EXIT_CODE=${PIPESTATUS[0]}
fi

set -e

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo

# Parse test output to extract results
echo -e "${BOLD}📊 TEST RESULTS SUMMARY${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Extract test results from output - handle both indented and non-indented format
TOTAL_TESTS=$(grep -E "^\s*Total tests:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Total tests:\s*//' | awk '{print $1}')
PASSED_TESTS=$(grep -E "^\s*Passed:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Passed:\s*//' | awk '{print $1}')
FAILED_TESTS=$(grep -E "^\s*Failed:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Failed:\s*//' | awk '{print $1}')
SKIPPED_TESTS=$(grep -E "^\s*Skipped:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Skipped:\s*//' | awk '{print $1}')

# Default values if parsing fails
TOTAL_TESTS=${TOTAL_TESTS:-0}
PASSED_TESTS=${PASSED_TESTS:-0}
FAILED_TESTS=${FAILED_TESTS:-0}
SKIPPED_TESTS=${SKIPPED_TESTS:-0}

# Display overall stats - check TEST_EXIT_CODE for actual pass/fail
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✅ OVERALL: PASSED${NC}"
else
    echo -e "${RED}❌ OVERALL: FAILED${NC}"
fi

echo
echo -e "Total Tests:    ${BOLD}$TOTAL_TESTS${NC}"
echo -e "Passed:         ${GREEN}$PASSED_TESTS${NC}"
if [ "$FAILED_TESTS" != "0" ] && [ -n "$FAILED_TESTS" ]; then
    echo -e "Failed:         ${RED}$FAILED_TESTS${NC}"
fi
if [ "$SKIPPED_TESTS" != "0" ] && [ -n "$SKIPPED_TESTS" ]; then
    echo -e "Skipped:        ${YELLOW}$SKIPPED_TESTS${NC}"
fi

# Find all new reports generated during this test run
echo
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BOLD}📄 TEST REPORTS${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

if [ -d "$REPORT_DIR" ]; then
    REPORTS_AFTER=$(ls -t "$REPORT_DIR"/*.md 2>/dev/null | head -20)
    
    # Find new reports by comparing before and after
    if [ -n "$REPORTS_BEFORE" ]; then
        NEW_REPORTS=$(comm -13 <(echo "$REPORTS_BEFORE" | sort) <(echo "$REPORTS_AFTER" | sort) 2>/dev/null)
    else
        # If no reports before, all current reports are new
        NEW_REPORTS="$REPORTS_AFTER"
    fi
    
    # Also check if running all providers - find reports from current test run (within 2 minutes)
    if [ "$PROVIDER_ARG" = "all" ] && [ -n "$REPORTS_AFTER" ]; then
        # Find all reports modified within the last 2 minutes (format: test_run_PROVIDER_YYYYMMDD_HHMMSS_fff_TESTID.md)
        CURRENT_RUN_REPORTS=$(find "$REPORT_DIR" -name "test_run_*.md" -mmin -2 -type f 2>/dev/null | sort -r)
        if [ -n "$CURRENT_RUN_REPORTS" ]; then
            NEW_REPORTS="$CURRENT_RUN_REPORTS"
        fi
    fi
    
    if [ -n "$NEW_REPORTS" ]; then
        # Parse each report to show provider-specific results
        for report in $NEW_REPORTS; do
            if [ -f "$report" ]; then
                # Extract provider name and status from report
                PROVIDER=$(grep -E "^### .* Provider:" "$report" 2>/dev/null | head -1 | sed 's/.*Provider: *//')
                STATUS=$(grep -E "^- Test Status:" "$report" 2>/dev/null | head -1)
                
                if [ -z "$PROVIDER" ]; then
                    # Try alternative format
                    PROVIDER=$(grep -E "^- Provider:" "$report" 2>/dev/null | head -1 | sed 's/.*Provider: *//')
                fi
                
                # Display provider result
                if [ -n "$PROVIDER" ]; then
                    if echo "$STATUS" | grep -q "PASSED"; then
                        echo -e "${GREEN}✅ $PROVIDER${NC}"
                    elif echo "$STATUS" | grep -q "FAILED"; then
                        echo -e "${RED}❌ $PROVIDER${NC}"
                    else
                        echo -e "${YELLOW}⚠️  $PROVIDER (unknown status)${NC}"
                    fi
                    echo -e "   Report: ${YELLOW}$report${NC}"
                    
                    # Extract any errors from the report
                    ERRORS=$(grep -E "^- (Error|Multimodal not supported):" "$report" 2>/dev/null | head -2)
                    if [ -n "$ERRORS" ]; then
                        echo "$ERRORS" | while IFS= read -r line; do
                            echo -e "   ${RED}$line${NC}"
                        done
                    fi
                    echo
                fi
            fi
        done
        
        # Quick access commands
        echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        echo -e "${BOLD}Quick Commands:${NC}"
        echo -e "  View all reports:  ${YELLOW}ls -la $REPORT_DIR/*.md${NC}"
        echo -e "  Open in VS Code:   ${YELLOW}code $REPORT_DIR/*.md${NC}"
    else
        # Fallback to showing the latest report if we can't determine new ones
        LATEST_REPORT=$(ls -t "$REPORT_DIR"/*.md 2>/dev/null | head -n1)
        if [ -n "$LATEST_REPORT" ]; then
            echo -e "${YELLOW}Latest report: $LATEST_REPORT${NC}"
        else
            echo -e "${YELLOW}⚠️  No test reports found${NC}"
        fi
    fi
else
    echo -e "${YELLOW}⚠️  Report directory not found: $REPORT_DIR${NC}"
fi

# Clean up temp file
rm -f "$TEST_OUTPUT_FILE"

echo

# Final status message
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✅ Test run completed successfully!${NC}"
else
    echo -e "${RED}${BOLD}❌ Test run completed with failures. Review the reports above.${NC}"
fi

exit $TEST_EXIT_CODE