#!/bin/bash

# Script to generate coverage badges for README
# This script should be run after coverage reports are generated

set -e

COVERAGE_DIR="./CoverageReport"
BADGES_DIR="./docs/badges"

# Create badges directory if it doesn't exist
mkdir -p "$BADGES_DIR"

echo "Generating coverage badges..."

if [ ! -f "$COVERAGE_DIR/Summary.json" ]; then
    echo "❌ Coverage summary not found at $COVERAGE_DIR/Summary.json"
    echo "Please run tests with coverage first: dotnet test --collect:\"XPlat Code Coverage\""
    exit 1
fi

# Extract coverage percentages
LINE_COVERAGE=$(jq -r '.summary.linecoverage' "$COVERAGE_DIR/Summary.json" 2>/dev/null || echo "0")
BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage' "$COVERAGE_DIR/Summary.json" 2>/dev/null || echo "0")
METHOD_COVERAGE=$(jq -r '.summary.methodcoverage' "$COVERAGE_DIR/Summary.json" 2>/dev/null || echo "0")

echo "Line Coverage: $LINE_COVERAGE%"
echo "Branch Coverage: $BRANCH_COVERAGE%"
echo "Method Coverage: $METHOD_COVERAGE%"

# Function to determine badge color based on percentage
get_badge_color() {
    local percentage=$1
    if (( $(echo "$percentage >= 80" | bc -l) )); then
        echo "brightgreen"
    elif (( $(echo "$percentage >= 60" | bc -l) )); then
        echo "yellow"
    elif (( $(echo "$percentage >= 40" | bc -l) )); then
        echo "orange"
    else
        echo "red"
    fi
}

# Generate badge URLs
LINE_COLOR=$(get_badge_color "$LINE_COVERAGE")
BRANCH_COLOR=$(get_badge_color "$BRANCH_COVERAGE")
METHOD_COLOR=$(get_badge_color "$METHOD_COVERAGE")

# Create badge markdown
cat > "$BADGES_DIR/coverage-badges.md" << EOF
<!-- Auto-generated coverage badges -->
[![Line Coverage](https://img.shields.io/badge/Line%20Coverage-${LINE_COVERAGE}%25-${LINE_COLOR})](https://github.com/knnlabs/Conduit/actions)
[![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-${BRANCH_COVERAGE}%25-${BRANCH_COLOR})](https://github.com/knnlabs/Conduit/actions)
[![Method Coverage](https://img.shields.io/badge/Method%20Coverage-${METHOD_COVERAGE}%25-${METHOD_COLOR})](https://github.com/knnlabs/Conduit/actions)
EOF

# Generate coverage summary for README
cat > "$BADGES_DIR/coverage-summary.md" << EOF
## 📊 Code Coverage

| Metric | Coverage |
|--------|----------|
| **Line Coverage** | ${LINE_COVERAGE}% |
| **Branch Coverage** | ${BRANCH_COVERAGE}% |
| **Method Coverage** | ${METHOD_COVERAGE}% |

### Coverage by Project

EOF

# Add project-specific coverage
jq -r '.coverage[] | select(.name | contains("ConduitLLM")) | "| **\(.name)** | \(.linecoverage)% |"' "$COVERAGE_DIR/Summary.json" 2>/dev/null >> "$BADGES_DIR/coverage-summary.md" || echo "| Coverage details unavailable | N/A |" >> "$BADGES_DIR/coverage-summary.md"

echo ""
echo "✅ Coverage badges generated:"
echo "   - $BADGES_DIR/coverage-badges.md"
echo "   - $BADGES_DIR/coverage-summary.md"
echo ""
echo "Add the following to your README.md:"
echo ""
cat "$BADGES_DIR/coverage-badges.md"