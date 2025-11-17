#!/bin/bash
# CI Build and Test Wrapper
# Provides robust error handling and clear output for GitHub Actions

set -euo pipefail

# Colors for local testing (disabled in CI)
if [ -t 1 ] && [ -z "${CI:-}" ]; then
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

# Configuration
COVERAGE_DIR="./CoverageReport"
TEST_RESULTS_DIR="./TestResults"
BUILD_CONFIG="${BUILD_CONFIG:-Release}"
COVERAGE_THRESHOLD_WARNING=40  # Warn if below this
COVERAGE_THRESHOLD_INFO=60     # Info if below this

# Summary variables
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0
SKIPPED_TESTS=0
BUILD_STATUS="success"
COVERAGE_STATUS="unknown"

# Helper functions
log_step() {
    echo -e "\n${BLUE}==>${NC} $1"
}

log_error() {
    echo -e "${RED}ERROR:${NC} $1" >&2
}

log_warning() {
    echo -e "${YELLOW}WARNING:${NC} $1"
}

log_success() {
    echo -e "${GREEN}SUCCESS:${NC} $1"
}

# Clean previous results
log_step "Cleaning previous test results"
rm -rf "$TEST_RESULTS_DIR" "$COVERAGE_DIR"
mkdir -p "$TEST_RESULTS_DIR" "$COVERAGE_DIR"

# Build
log_step "Building solution"
if ! dotnet build --configuration "$BUILD_CONFIG" --no-incremental; then
    log_error "Build failed!"
    BUILD_STATUS="failed"
    exit 1
fi
log_success "Build completed successfully"

# Run tests
log_step "Running tests with coverage"
TEST_EXIT_CODE=0
dotnet test \
    --no-build \
    --configuration "$BUILD_CONFIG" \
    --logger "trx" \
    --logger "console;verbosity=minimal" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$TEST_RESULTS_DIR" \
    --settings .runsettings \
    -- RunConfiguration.TreatNoTestsAsError=false || TEST_EXIT_CODE=$?

# Parse test results from console output (more reliable than TRX parsing)
if [ -f "$TEST_RESULTS_DIR/test-output.log" ]; then
    # dotnet test provides a summary at the end we can parse
    TOTAL_TESTS=$(grep -E "Total tests: [0-9]+" "$TEST_RESULTS_DIR/test-output.log" | grep -o "[0-9]+" | tail -1 || echo "0")
    PASSED_TESTS=$(grep -E "Passed: [0-9]+" "$TEST_RESULTS_DIR/test-output.log" | grep -o "[0-9]+" | tail -1 || echo "0")
    FAILED_TESTS=$(grep -E "Failed: [0-9]+" "$TEST_RESULTS_DIR/test-output.log" | grep -o "[0-9]+" | tail -1 || echo "0")
    SKIPPED_TESTS=$(grep -E "Skipped: [0-9]+" "$TEST_RESULTS_DIR/test-output.log" | grep -o "[0-9]+" | tail -1 || echo "0")
fi

# Generate coverage report
log_step "Generating coverage report"
COVERAGE_FILES=$(find "$TEST_RESULTS_DIR" -name "coverage.cobertura.xml" -type f)
if [ -n "$COVERAGE_FILES" ]; then
    dotnet tool run reportgenerator \
        -reports:"$TEST_RESULTS_DIR/**/coverage.cobertura.xml" \
        -targetdir:"$COVERAGE_DIR" \
        -reporttypes:"JsonSummary;Badges" \
        -verbosity:Warning \
        -title:"Conduit Coverage Report" \
        -tag:"${GITHUB_RUN_NUMBER:-local}" || {
            log_warning "Coverage report generation failed"
            COVERAGE_STATUS="failed"
        }
    
    # Extract coverage metrics
    if [ -f "$COVERAGE_DIR/Summary.json" ]; then
        LINE_COVERAGE=$(jq -r '.summary.linecoverage // 0' "$COVERAGE_DIR/Summary.json")
        BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage // 0' "$COVERAGE_DIR/Summary.json")
        METHOD_COVERAGE=$(jq -r '.summary.methodcoverage // 0' "$COVERAGE_DIR/Summary.json")
        COVERAGE_STATUS="success"
        
        # Determine coverage level
        if (( $(echo "$LINE_COVERAGE < $COVERAGE_THRESHOLD_WARNING" | bc -l) )); then
            log_warning "Line coverage is low: ${LINE_COVERAGE}%"
        elif (( $(echo "$LINE_COVERAGE < $COVERAGE_THRESHOLD_INFO" | bc -l) )); then
            echo "Line coverage: ${LINE_COVERAGE}% (improving needed)"
        else
            log_success "Line coverage: ${LINE_COVERAGE}%"
        fi
    fi
else
    log_warning "No coverage files found"
    COVERAGE_STATUS="none"
fi

# Generate summary for GitHub Actions
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
        echo "# 📊 Build & Test Summary"
        echo ""
        echo "## 🔨 Build"
        echo "- **Status**: $([ "$BUILD_STATUS" = "success" ] && echo "✅ Success" || echo "❌ Failed")"
        echo "- **Configuration**: $BUILD_CONFIG"
        echo ""
        echo "## 🧪 Tests"
        if [ $TOTAL_TESTS -gt 0 ]; then
            echo "- **Total**: $TOTAL_TESTS"
            echo "- **Passed**: ✅ $PASSED_TESTS"
            echo "- **Failed**: ❌ $FAILED_TESTS"
            echo "- **Skipped**: ⏭️ $SKIPPED_TESTS"
            if [ $FAILED_TESTS -gt 0 ]; then
                echo ""
                echo "⚠️ **Some tests failed. Check the logs for details.**"
            fi
        else
            echo "⚠️ No test results found"
        fi
        echo ""
        echo "## 📈 Coverage"
        if [ "$COVERAGE_STATUS" = "success" ]; then
            echo "- **Line**: ${LINE_COVERAGE}%"
            echo "- **Branch**: ${BRANCH_COVERAGE}%"
            echo "- **Method**: ${METHOD_COVERAGE}%"
            echo ""
            # Add visual indicator
            if (( $(echo "$LINE_COVERAGE >= 80" | bc -l) )); then
                echo "🟢 Excellent coverage!"
            elif (( $(echo "$LINE_COVERAGE >= 60" | bc -l) )); then
                echo "🟡 Good coverage, room for improvement"
            elif (( $(echo "$LINE_COVERAGE >= 40" | bc -l) )); then
                echo "🟠 Fair coverage, needs improvement"
            else
                echo "🔴 Low coverage, please add more tests"
            fi
        else
            echo "❌ Coverage data not available"
        fi
        echo ""
        echo "---"
        echo "*Generated at $(date -u '+%Y-%m-%d %H:%M:%S UTC')*"
    } >> "$GITHUB_STEP_SUMMARY"
fi

# Exit based on test results (not coverage)
if [ $TEST_EXIT_CODE -ne 0 ]; then
    log_error "Tests failed!"
    exit $TEST_EXIT_CODE
fi

log_success "All tests passed!"
exit 0