#!/bin/bash

# Coverage Threshold Checker
# Used by CI/CD to enforce minimum coverage requirements

set -e

COVERAGE_REPORT="./CoverageReport/Summary.json"
EXIT_CODE=0

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo_colored() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Check if coverage report exists
if [ ! -f "$COVERAGE_REPORT" ]; then
    echo_colored "$RED" "❌ Coverage report not found at $COVERAGE_REPORT"
    echo "Please run tests with coverage collection first."
    exit 1
fi

# Extract coverage metrics
LINE_COVERAGE=$(jq -r '.summary.linecoverage' "$COVERAGE_REPORT" 2>/dev/null || echo "0")
BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage' "$COVERAGE_REPORT" 2>/dev/null || echo "0")
METHOD_COVERAGE=$(jq -r '.summary.methodcoverage' "$COVERAGE_REPORT" 2>/dev/null || echo "0")

echo "Coverage Threshold Check"
echo "======================="
echo "Line Coverage:   $LINE_COVERAGE%"
echo "Branch Coverage: $BRANCH_COVERAGE%"
echo "Method Coverage: $METHOD_COVERAGE%"
echo ""

# Define thresholds (these can be gradually increased)
MIN_LINE_COVERAGE=40
MIN_BRANCH_COVERAGE=30
MIN_METHOD_COVERAGE=40

# Check overall coverage
check_threshold() {
    local metric_name=$1
    local actual=$2
    local threshold=$3
    
    if (( $(echo "$actual >= $threshold" | bc -l) )); then
        echo_colored "$GREEN" "✅ $metric_name: $actual% (>= $threshold%)"
    else
        echo_colored "$RED" "❌ $metric_name: $actual% (< $threshold%)"
        EXIT_CODE=1
    fi
}

echo "Threshold Check Results:"
check_threshold "Line Coverage" "$LINE_COVERAGE" "$MIN_LINE_COVERAGE"
check_threshold "Branch Coverage" "$BRANCH_COVERAGE" "$MIN_BRANCH_COVERAGE"
check_threshold "Method Coverage" "$METHOD_COVERAGE" "$MIN_METHOD_COVERAGE"

echo ""

# Check critical service coverage
echo "Critical Service Analysis:"
echo "=========================="

check_service_coverage() {
    local service_name=$1
    local service_pattern=$2
    local min_threshold=$3
    
    local coverage=$(jq -r ".coverage.assemblies[] | select(.name | contains(\"$service_pattern\")) | .coverage" "$COVERAGE_REPORT" 2>/dev/null)
    
    if [ -z "$coverage" ] || [ "$coverage" = "null" ]; then
        echo_colored "$YELLOW" "⚠️  $service_name: No coverage data found"
        return
    fi
    
    if (( $(echo "$coverage >= $min_threshold" | bc -l) )); then
        echo_colored "$GREEN" "✅ $service_name: $coverage% (>= $min_threshold%)"
    else
        echo_colored "$RED" "❌ $service_name: $coverage% (< $min_threshold%)"
        echo "   This is a critical service that requires higher coverage!"
        EXIT_CODE=1
    fi
}

# Critical services with their minimum thresholds
check_service_coverage "Core Services" "ConduitLLM.Core" 40
check_service_coverage "HTTP API" "ConduitLLM.Http" 35
check_service_coverage "Admin API" "ConduitLLM.Admin" 35

echo ""

# Final result
if [ $EXIT_CODE -eq 0 ]; then
    echo_colored "$GREEN" "🎉 All coverage thresholds passed!"
    echo "Your changes maintain adequate test coverage."
else
    echo_colored "$RED" "💥 Coverage thresholds not met!"
    echo ""
    echo "To fix this:"
    echo "1. Add unit tests for uncovered code"
    echo "2. Focus on critical services (Core, HTTP, Admin)"
    echo "3. Ensure new features include comprehensive tests"
    echo "4. Run './scripts/coverage-dashboard.sh run' to see detailed coverage"
fi

exit $EXIT_CODE