#!/bin/bash
# Coverage Information Script (Non-blocking)
# Provides coverage insights without failing the build

set -e

COVERAGE_REPORT="./CoverageReport/Summary.json"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo_colored() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Check if coverage report exists
if [ ! -f "$COVERAGE_REPORT" ]; then
    echo_colored "$YELLOW" "⚠️  Coverage report not found - skipping coverage analysis"
    exit 0  # Exit successfully - don't block the build
fi

# Extract coverage metrics
LINE_COVERAGE=$(jq -r '.summary.linecoverage // 0' "$COVERAGE_REPORT")
BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage // 0' "$COVERAGE_REPORT")
METHOD_COVERAGE=$(jq -r '.summary.methodcoverage // 0' "$COVERAGE_REPORT")

echo_colored "$BLUE" "📊 Coverage Report"
echo "=================="
echo "Line Coverage:   $LINE_COVERAGE%"
echo "Branch Coverage: $BRANCH_COVERAGE%"
echo "Method Coverage: $METHOD_COVERAGE%"
echo ""

# Coverage trends (informational)
echo_colored "$BLUE" "Coverage Analysis:"

# Function to provide friendly feedback
provide_coverage_feedback() {
    local metric_name=$1
    local actual=$2
    local good_threshold=60
    local excellent_threshold=80
    
    if (( $(echo "$actual >= $excellent_threshold" | bc -l) )); then
        echo_colored "$GREEN" "✨ $metric_name: $actual% - Excellent!"
    elif (( $(echo "$actual >= $good_threshold" | bc -l) )); then
        echo_colored "$GREEN" "✅ $metric_name: $actual% - Good"
    elif (( $(echo "$actual >= 40" | bc -l) )); then
        echo_colored "$YELLOW" "📈 $metric_name: $actual% - Room for improvement"
    else
        echo_colored "$YELLOW" "📊 $metric_name: $actual% - Consider adding tests"
    fi
}

provide_coverage_feedback "Line Coverage" "$LINE_COVERAGE"
provide_coverage_feedback "Branch Coverage" "$BRANCH_COVERAGE"
provide_coverage_feedback "Method Coverage" "$METHOD_COVERAGE"

echo ""

# Service-specific coverage (informational)
echo_colored "$BLUE" "Service Coverage:"
echo "================="

check_service_coverage() {
    local service_name=$1
    local service_pattern=$2
    
    local coverage=$(jq -r ".coverage.assemblies[] | select(.name | contains(\"$service_pattern\")) | .coverage" "$COVERAGE_REPORT" 2>/dev/null)
    
    if [ -z "$coverage" ] || [ "$coverage" = "null" ]; then
        echo "  $service_name: No data"
    else
        if (( $(echo "$coverage >= 40" | bc -l) )); then
            echo_colored "$GREEN" "  $service_name: $coverage%"
        else
            echo_colored "$YELLOW" "  $service_name: $coverage% (consider adding tests)"
        fi
    fi
}

check_service_coverage "Core Services" "ConduitLLM.Core"
check_service_coverage "HTTP API" "ConduitLLM.Http"
check_service_coverage "Admin API" "ConduitLLM.Admin"

echo ""

# Coverage trend suggestion
if (( $(echo "$LINE_COVERAGE < 40" | bc -l) )); then
    echo_colored "$BLUE" "💡 Coverage Tips:"
    echo "  • Focus on testing critical business logic first"
    echo "  • Consider adding unit tests for new features"
    echo "  • Use 'dotnet test' locally to check coverage"
    echo "  • Run './scripts/coverage-dashboard.sh' for detailed analysis"
fi

# Always exit successfully
echo ""
echo_colored "$GREEN" "✅ Coverage analysis complete (informational only)"
exit 0