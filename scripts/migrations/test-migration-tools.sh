#!/bin/bash
set -e

# Script: test-migration-tools.sh
# Purpose: Test the migration validation tools in various scenarios

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Test results
TESTS_PASSED=0
TESTS_FAILED=0

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"

print_test_header() {
    echo ""
    echo -e "${BLUE}TEST:${NC} $1"
    echo "------------------------------"
}

print_result() {
    local status=$1
    local message=$2
    
    if [ "$status" = "PASS" ]; then
        echo -e "${GREEN}✓ PASS:${NC} $message"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ FAIL:${NC} $message"
        TESTS_FAILED=$((TESTS_FAILED + 1))
    fi
}

# Test 1: Test ef-wrapper without DATABASE_URL
test_wrapper_no_database_url() {
    print_test_header "EF Wrapper - No DATABASE_URL"
    
    cd "$PROJECT_ROOT/ConduitLLM.Configuration"
    
    # Temporarily unset DATABASE_URL
    local saved_db_url="$DATABASE_URL"
    unset DATABASE_URL
    
    # Run wrapper and expect it to fail gracefully
    if "$SCRIPT_DIR/ef-wrapper.sh" migrations list --no-build 2>&1 | grep -q "DATABASE_URL environment variable is not set"; then
        print_result "PASS" "Wrapper correctly detected missing DATABASE_URL"
    else
        print_result "FAIL" "Wrapper did not detect missing DATABASE_URL"
    fi
    
    # Restore DATABASE_URL
    export DATABASE_URL="$saved_db_url"
}

# Test 2: Test ef-wrapper with invalid DATABASE_URL format
test_wrapper_invalid_database_url() {
    print_test_header "EF Wrapper - Invalid DATABASE_URL Format"
    
    cd "$PROJECT_ROOT/ConduitLLM.Configuration"
    
    # Set invalid DATABASE_URL
    local saved_db_url="$DATABASE_URL"
    export DATABASE_URL="invalid-connection-string"
    
    # Run wrapper and check for warning
    if "$SCRIPT_DIR/ef-wrapper.sh" migrations list --no-build 2>&1 | grep -q "DATABASE_URL format may be invalid"; then
        print_result "PASS" "Wrapper warned about invalid DATABASE_URL format"
    else
        print_result "FAIL" "Wrapper did not warn about invalid DATABASE_URL format"
    fi
    
    # Restore DATABASE_URL
    export DATABASE_URL="$saved_db_url"
}

# Test 3: Test ef-wrapper from wrong directory
test_wrapper_wrong_directory() {
    print_test_header "EF Wrapper - Wrong Directory"
    
    cd "$PROJECT_ROOT"
    
    # Run wrapper from wrong directory
    if "$SCRIPT_DIR/ef-wrapper.sh" migrations list --no-build 2>&1 | grep -q "Not in ConduitLLM.Configuration directory"; then
        print_result "PASS" "Wrapper detected wrong directory"
    else
        print_result "FAIL" "Wrapper did not detect wrong directory"
    fi
}

# Test 4: Test validate-migrations.sh basic functionality
test_validate_migrations_basic() {
    print_test_header "Validate Migrations - Basic Run"
    
    cd "$PROJECT_ROOT/ConduitLLM.Configuration"
    
    # Run validation script and capture output
    local output=$("$SCRIPT_DIR/validate-migrations.sh" 2>&1)
    local exit_code=$?
    
    # Check if it ran (even if it found issues)
    if echo "$output" | grep -q "EF Core Migration Validation"; then
        if [ $exit_code -eq 0 ]; then
            print_result "PASS" "Validation script completed successfully"
        else
            # Script ran but found issues - this is still correct behavior
            if echo "$output" | grep -q "ERROR:"; then
                print_result "PASS" "Validation script correctly detected migration issues"
                echo "  Note: Found migration issues (expected behavior)"
            else
                print_result "FAIL" "Validation script failed unexpectedly"
            fi
        fi
    else
        print_result "FAIL" "Validation script did not run properly"
    fi
}

# Test 5: Test GitHub Actions workflow syntax
test_github_actions_syntax() {
    print_test_header "GitHub Actions Workflow - Syntax Check"
    
    # Check if workflow file has consistent DATABASE_URL usage
    local workflow_file="$PROJECT_ROOT/.github/workflows/migration-validation.yml"
    
    # Check job-level env is defined
    if grep -q "env:" "$workflow_file" && grep -A 5 "validate-migrations:" "$workflow_file" | grep -q "DATABASE_URL:"; then
        print_result "PASS" "Workflow has job-level DATABASE_URL defined"
    else
        print_result "FAIL" "Workflow missing job-level DATABASE_URL"
    fi
    
    # Check no duplicate env declarations in steps
    local duplicate_count=$(grep -A 2 "env:" "$workflow_file" | grep -c "DATABASE_URL:" || echo 0)
    if [ "$duplicate_count" -eq 1 ]; then
        print_result "PASS" "No duplicate DATABASE_URL declarations"
    else
        print_result "FAIL" "Found $duplicate_count DATABASE_URL declarations (expected 1)"
    fi
}

# Test 6: Test if all required scripts are executable
test_scripts_executable() {
    print_test_header "Script Permissions"
    
    local scripts=(
        "$SCRIPT_DIR/validate-migrations.sh"
        "$SCRIPT_DIR/ef-wrapper.sh"
        "$SCRIPT_DIR/test-migration-tools.sh"
    )
    
    local all_executable=true
    for script in "${scripts[@]}"; do
        if [ -x "$script" ]; then
            echo -e "  ${GREEN}✓${NC} $script is executable"
        else
            echo -e "  ${RED}✗${NC} $script is NOT executable"
            all_executable=false
        fi
    done
    
    if [ "$all_executable" = true ]; then
        print_result "PASS" "All scripts are executable"
    else
        print_result "FAIL" "Some scripts are not executable"
    fi
}

# Test 7: Test that ef-wrapper provides better error messages
test_wrapper_error_messages() {
    print_test_header "EF Wrapper - Enhanced Error Messages"
    
    cd "$PROJECT_ROOT/ConduitLLM.Configuration"
    
    # Test with a command that will fail
    local output=$("$SCRIPT_DIR/ef-wrapper.sh" migrations add TestMigration --no-build 2>&1 || true)
    
    # Check if wrapper provides helpful context
    if echo "$output" | grep -q "Validating environment" && echo "$output" | grep -q "EF Core Command Wrapper"; then
        print_result "PASS" "Wrapper provides structured output with validation"
    else
        print_result "FAIL" "Wrapper output lacks structure or validation info"
    fi
}

# Main execution
main() {
    echo "=============================================="
    echo "Migration Tools Test Suite"
    echo "=============================================="
    echo "Running comprehensive tests..."
    
    # Save current DATABASE_URL
    ORIGINAL_DATABASE_URL="$DATABASE_URL"
    
    # Run all tests
    test_wrapper_no_database_url
    test_wrapper_invalid_database_url
    test_wrapper_wrong_directory
    test_validate_migrations_basic
    test_github_actions_syntax
    test_scripts_executable
    test_wrapper_error_messages
    
    # Restore DATABASE_URL
    export DATABASE_URL="$ORIGINAL_DATABASE_URL"
    
    # Summary
    echo ""
    echo "=============================================="
    echo "Test Summary"
    echo "=============================================="
    echo -e "${GREEN}Passed:${NC} $TESTS_PASSED"
    echo -e "${RED}Failed:${NC} $TESTS_FAILED"
    echo -e "Total:  $((TESTS_PASSED + TESTS_FAILED))"
    
    if [ $TESTS_FAILED -eq 0 ]; then
        echo ""
        echo -e "${GREEN}✓ All tests passed!${NC}"
        exit 0
    else
        echo ""
        echo -e "${RED}✗ Some tests failed${NC}"
        exit 1
    fi
}

# Run main
main "$@"