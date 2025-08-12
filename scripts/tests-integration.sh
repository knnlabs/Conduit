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
DIM='\033[2m' # Dimmed text

# Provider configuration
PROVIDER_ARG="${1:-all}"
SUPPORTED_PROVIDERS=("groq" "sambanova" "cerebras")

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
                "cerebras")
                    echo -e "${BLUE}   📝 Opening in VS Code for you to add your Cerebras API key...${NC}"
                    echo -e "${YELLOW}   Look for: apiKey: \"YOUR_CEREBRAS_API_KEY_HERE\"${NC}"
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
        "cerebras")
            if grep -q "YOUR_CEREBRAS_API_KEY_HERE" "$provider_config" 2>/dev/null; then
                echo -e "${RED}❌ Cerebras API key not configured!${NC}"
                echo -e "${BLUE}   Opening in VS Code for editing...${NC}"
                if command -v code >/dev/null 2>&1; then
                    code "$TEST_DIR/$provider_config"
                    echo -e "${GREEN}   ✓ Opened in VS Code${NC}"
                else
                    echo -e "${YELLOW}   Edit manually: $TEST_DIR/$provider_config${NC}"
                fi
                echo -e "${YELLOW}   Replace: YOUR_CEREBRAS_API_KEY_HERE with your actual API key${NC}"
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
        "cerebras")
            dotnet test --no-build --filter "FullyQualifiedName~CerebrasEndToEndTest" --logger "console;verbosity=normal"
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
PASSED_TESTS_RAW=$(grep -E "^\s*Passed:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Passed:\s*//' | awk '{print $1}')
FAILED_TESTS=$(grep -E "^\s*Failed:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Failed:\s*//' | awk '{print $1}')
SKIPPED_TESTS_RAW=$(grep -E "^\s*Skipped:" "$TEST_OUTPUT_FILE" 2>/dev/null | tail -1 | sed 's/.*Skipped:\s*//' | awk '{print $1}')

# Count tests with "ShouldSkip" in the name as skipped, not passed
SHOULDSKIP_COUNT=$(grep -E "Passed.*ShouldSkip" "$TEST_OUTPUT_FILE" 2>/dev/null | wc -l)

# Adjust counts: subtract ShouldSkip tests from passed and add to skipped
PASSED_TESTS=$((${PASSED_TESTS_RAW:-0} - ${SHOULDSKIP_COUNT:-0}))
SKIPPED_TESTS=$((${SKIPPED_TESTS_RAW:-0} + ${SHOULDSKIP_COUNT:-0}))

# Default values if parsing fails
TOTAL_TESTS=${TOTAL_TESTS:-0}
PASSED_TESTS=${PASSED_TESTS:-0}
FAILED_TESTS=${FAILED_TESTS:-0}
SKIPPED_TESTS=${SKIPPED_TESTS:-0}

# Parse individual test results from xUnit output
declare -A TEST_RESULTS
declare -A TEST_ERRORS

# Parse xUnit test output for detailed results
# Look for patterns from xUnit output like:
#   Passed ConduitLLM.IntegrationTests.Tests.GroqEndToEndTest.GroqProvider_CompleteEndToEndFlow_ShouldWork
#   Failed ConduitLLM.IntegrationTests.Tests.CerebrasEndToEndTest.CerebrasProvider_BasicChat_ShouldWork
# Also handle xUnit.net format:
#   [xUnit.net 00:00:00.29]     Cerebras Provider - Basic Chat Test [FAIL]
while IFS= read -r line; do
    # First try standard dotnet test output format
    if [[ "$line" =~ ^[[:space:]]*(Passed|Failed|Skipped)[[:space:]]+ConduitLLM\.IntegrationTests\.Tests\.([^.]+)\.(.+)[[:space:]]*(\[|$) ]]; then
        STATUS="${BASH_REMATCH[1]}"
        TEST_CLASS="${BASH_REMATCH[2]}"
        TEST_METHOD="${BASH_REMATCH[3]}"
        # Remove timing info if present
        TEST_METHOD=$(echo "$TEST_METHOD" | sed 's/ \[.*$//')
        
        # Determine provider from test class name
        PROVIDER=""
        if [[ "$TEST_CLASS" =~ Groq ]]; then
            PROVIDER="Groq"
        elif [[ "$TEST_CLASS" =~ SambaNova ]]; then
            PROVIDER="SambaNova"
        elif [[ "$TEST_CLASS" =~ Cerebras ]]; then
            PROVIDER="Cerebras"
        fi
        
        if [ -n "$PROVIDER" ]; then
            TEST_KEY="${PROVIDER}::${TEST_METHOD}"
            TEST_RESULTS["$TEST_KEY"]="$STATUS"
            
            # Capture error messages for failed tests
            if [ "$STATUS" = "Failed" ]; then
                # Look for the error message in the next few lines
                ERROR_MSG=$(grep -A 5 "Failed.*$TEST_METHOD" "$TEST_OUTPUT_FILE" 2>/dev/null | grep -E "(Error Message:|Message:)" | head -1 | sed 's/.*Message: *//')
                if [ -n "$ERROR_MSG" ]; then
                    TEST_ERRORS["$TEST_KEY"]="$ERROR_MSG"
                fi
            fi
        fi
    # Also check for xUnit.net format with [FAIL] indicator
    elif [[ "$line" =~ \[xUnit\.net[[:space:]]+[^\]]+\][[:space:]]+(.+)[[:space:]]+\[(FAIL|PASS|SKIP)\] ]]; then
        TEST_NAME="${BASH_REMATCH[1]}"
        STATUS_INDICATOR="${BASH_REMATCH[2]}"
        
        # Map status indicator to status
        case "$STATUS_INDICATOR" in
            "FAIL")
                STATUS="Failed"
                ;;
            "PASS")
                STATUS="Passed"
                ;;
            "SKIP")
                STATUS="Skipped"
                ;;
        esac
        
        # Determine provider and method from test name
        PROVIDER=""
        METHOD_NAME=""
        if [[ "$TEST_NAME" =~ Cerebras ]]; then
            PROVIDER="Cerebras"
            METHOD_NAME="CerebrasProvider_BasicChat_ShouldWork"
        elif [[ "$TEST_NAME" =~ Groq ]]; then
            PROVIDER="Groq"
            METHOD_NAME="GroqProvider_CompleteEndToEndFlow_ShouldWork"
        elif [[ "$TEST_NAME" =~ SambaNova ]]; then
            PROVIDER="SambaNova"
            if [[ "$TEST_NAME" =~ Multimodal ]]; then
                METHOD_NAME="SambaNovaProvider_MultimodalChat_ShouldWork"
            else
                METHOD_NAME="SambaNovaProvider_BasicChat_ShouldWork"
            fi
        fi
        
        if [ -n "$PROVIDER" ] && [ -n "$METHOD_NAME" ]; then
            TEST_KEY="${PROVIDER}::${METHOD_NAME}"
            TEST_RESULTS["$TEST_KEY"]="$STATUS"
            
            # Capture error for failed tests
            if [ "$STATUS" = "Failed" ]; then
                # Look for error message after this line
                ERROR_MSG=$(grep -A 3 "$TEST_NAME.*\[FAIL\]" "$TEST_OUTPUT_FILE" 2>/dev/null | grep -E "Exception" | head -1 | sed 's/.*Exception : //')
                if [ -n "$ERROR_MSG" ]; then
                    TEST_ERRORS["$TEST_KEY"]="$ERROR_MSG"
                fi
            fi
        fi
    fi
done < "$TEST_OUTPUT_FILE"

# Display overall stats - check TEST_EXIT_CODE for actual pass/fail
if [ $TEST_EXIT_CODE -eq 0 ] && [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}✅ OVERALL: ALL TESTS PASSED${NC}"
elif [ $FAILED_TESTS -gt 0 ]; then
    echo -e "${RED}❌ OVERALL: ${FAILED_TESTS} TEST(S) FAILED${NC}"
else
    echo -e "${YELLOW}⚠️ OVERALL: COMPLETED WITH WARNINGS${NC}"
fi

echo
echo -e "Total Tests:    ${BOLD}$TOTAL_TESTS${NC}"
if [ $PASSED_TESTS -gt 0 ]; then
    echo -e "Passed:         ${GREEN}$PASSED_TESTS${NC}"
fi
if [ "$FAILED_TESTS" != "0" ] && [ -n "$FAILED_TESTS" ]; then
    echo -e "Failed:         ${RED}$FAILED_TESTS${NC}"
fi
if [ "$SKIPPED_TESTS" != "0" ] && [ -n "$SKIPPED_TESTS" ]; then
    echo -e "Skipped:        ${YELLOW}$SKIPPED_TESTS${NC}"
fi

# Find all new reports generated during this test run
echo
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BOLD}📄 DETAILED TEST RESULTS BY PROVIDER${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Display test results from parsed xUnit output
if [ ${#TEST_RESULTS[@]} -gt 0 ]; then
    # Group results by provider
    declare -A PROVIDER_STATS
    
    for TEST_KEY in "${!TEST_RESULTS[@]}"; do
        PROVIDER="${TEST_KEY%%::*}"
        TEST_METHOD="${TEST_KEY#*::}"
        STATUS="${TEST_RESULTS[$TEST_KEY]}"
        
        # Initialize provider stats if not exists
        if [ -z "${PROVIDER_STATS[$PROVIDER]}" ]; then
            PROVIDER_STATS[$PROVIDER]="0:0:0"  # passed:failed:skipped
        fi
        
        # Parse current stats
        IFS=':' read -r PASS FAIL SKIP <<< "${PROVIDER_STATS[$PROVIDER]}"
        
        # Special handling for tests with "ShouldSkip" in the name
        if [[ "$TEST_METHOD" =~ ShouldSkip ]]; then
            # These tests are skipped regardless of actual status
            SKIP=$((SKIP + 1))
        else
            # Update stats based on status
            case "$STATUS" in
                "Passed")
                    PASS=$((PASS + 1))
                    ;;
                "Failed")
                    FAIL=$((FAIL + 1))
                    ;;
                "Skipped")
                    SKIP=$((SKIP + 1))
                    ;;
            esac
        fi
        
        PROVIDER_STATS[$PROVIDER]="$PASS:$FAIL:$SKIP"
    done
    
    # Display results by provider
    IFS=$'\n' SORTED_PROVIDERS=($(sort <<<"${!PROVIDER_STATS[*]}"))
    unset IFS
    
    for PROVIDER in "${SORTED_PROVIDERS[@]}"; do
        IFS=':' read -r PASS FAIL SKIP <<< "${PROVIDER_STATS[$PROVIDER]}"
        TOTAL=$((PASS + FAIL + SKIP))
        
        # Calculate percentage
        if [ $TOTAL -gt 0 ]; then
            PASS_PERCENTAGE=$((PASS * 100 / TOTAL))
            FAIL_PERCENTAGE=$((FAIL * 100 / TOTAL))
        else
            PASS_PERCENTAGE=0
            FAIL_PERCENTAGE=0
        fi
        
        # Display provider header
        echo
        if [ $FAIL -eq 0 ] && [ $SKIP -eq 0 ]; then
            echo -e "${GREEN}✅ $PROVIDER${NC} - ${BOLD}${GREEN}100% PASSED${NC} ($PASS/$TOTAL tests)"
        elif [ $FAIL -gt 0 ] && [ $PASS -eq 0 ]; then
            echo -e "${RED}❌ $PROVIDER${NC} - ${BOLD}${RED}100% FAILED${NC} ($FAIL/$TOTAL tests)"
        elif [ $FAIL -gt 0 ]; then
            echo -e "${YELLOW}⚠️  $PROVIDER${NC} - ${BOLD}${GREEN}${PASS_PERCENTAGE}% Passed${NC}, ${BOLD}${RED}${FAIL_PERCENTAGE}% Failed${NC} ($PASS passed, $FAIL failed, $SKIP skipped)"
        elif [ $SKIP -gt 0 ]; then
            echo -e "${YELLOW}⚠️  $PROVIDER${NC} - ${BOLD}${GREEN}${PASS_PERCENTAGE}% Passed${NC}, ${BOLD}${YELLOW}Skipped: $SKIP${NC} ($PASS passed, $SKIP skipped)"
        fi
        
        # Display individual test results
        for TEST_KEY in "${!TEST_RESULTS[@]}"; do
            if [[ "$TEST_KEY" =~ ^${PROVIDER}:: ]]; then
                TEST_METHOD="${TEST_KEY#*::}"
                STATUS="${TEST_RESULTS[$TEST_KEY]}"
                
                # Format test method name for display
                DISPLAY_NAME=$(echo "$TEST_METHOD" | sed 's/_/ /g' | sed 's/Should/ Should/g')
                
                # Check if this is a test that should be skipped (has ShouldSkip in name)
                if [[ "$TEST_METHOD" =~ ShouldSkip ]]; then
                    # This is a skip test - always show as skipped regardless of status
                    echo -e "   ${YELLOW}⊘${NC} $DISPLAY_NAME ${DIM}(skipped)${NC}"
                else
                    case "$STATUS" in
                        "Passed")
                            echo -e "   ${GREEN}✓${NC} $DISPLAY_NAME"
                            ;;
                        "Failed")
                            echo -e "   ${RED}✗${NC} $DISPLAY_NAME"
                            if [ -n "${TEST_ERRORS[$TEST_KEY]}" ]; then
                                ERROR="${TEST_ERRORS[$TEST_KEY]}"
                                # Truncate long error messages
                                if [ ${#ERROR} -gt 80 ]; then
                                    ERROR="${ERROR:0:77}..."
                                fi
                                echo -e "      ${DIM}${ERROR}${NC}"
                            fi
                            ;;
                        "Skipped")
                            echo -e "   ${YELLOW}⊘${NC} $DISPLAY_NAME ${DIM}(skipped)${NC}"
                            ;;
                    esac
                fi
            fi
        done
    done
    echo
fi

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BOLD}📄 GENERATED TEST REPORTS${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Show generated report files (if any)
if [ -d "$REPORT_DIR" ]; then
    # Find reports from current test run (within 2 minutes)
    CURRENT_RUN_REPORTS=$(find "$REPORT_DIR" -name "test_run_*.md" -mmin -2 -type f 2>/dev/null | sort -r)
    
    if [ -n "$CURRENT_RUN_REPORTS" ]; then
        echo
        echo -e "Generated report files:"
        for report in $CURRENT_RUN_REPORTS; do
            REPORT_NAME=$(basename "$report")
            echo -e "  ${DIM}$REPORT_NAME${NC}"
        done
        
        echo
        echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        echo -e "${BOLD}Quick Commands:${NC}"
        echo -e "  View all reports:  ${YELLOW}ls -la $REPORT_DIR/*.md${NC}"
        echo -e "  Open in VS Code:   ${YELLOW}code $REPORT_DIR/*.md${NC}"
    fi
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