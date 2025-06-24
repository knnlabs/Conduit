#!/bin/bash

# Coverage Dashboard Script
# Generates comprehensive coverage reports and analysis

set -e

echo "🔍 ConduitLLM Coverage Dashboard"
echo "================================"

# Configuration
COVERAGE_DIR="./TestResults"
REPORT_DIR="./CoverageReport"
SCRIPTS_DIR="$(dirname "$0")"
PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo_colored() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to run tests with coverage
run_coverage() {
    echo_colored "$BLUE" "📊 Running tests with coverage collection..."
    
    # Clean previous results
    rm -rf "$COVERAGE_DIR" "$REPORT_DIR"
    
    # Restore tools if needed
    if [ ! -f ".config/dotnet-tools.json" ]; then
        echo_colored "$YELLOW" "⚠️  No local tools manifest found. Creating one..."
        mkdir -p .config
        cat > .config/dotnet-tools.json << 'EOF'
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-reportgenerator-globaltool": {
      "version": "5.3.11",
      "commands": [
        "reportgenerator"
      ]
    }
  }
}
EOF
    fi
    
    echo_colored "$BLUE" "📦 Restoring tools..."
    dotnet tool restore
    
    echo_colored "$BLUE" "🧪 Running tests..."
    dotnet test --configuration Release \
        --logger "console;verbosity=normal" \
        --collect:"XPlat Code Coverage" \
        --results-directory "$COVERAGE_DIR" \
        --settings .runsettings
    
    echo_colored "$GREEN" "✅ Tests completed"
}

# Function to generate reports
generate_reports() {
    echo_colored "$BLUE" "📋 Generating coverage reports..."
    
    # Find coverage files
    COVERAGE_FILES=$(find "$COVERAGE_DIR" -name "coverage.cobertura.xml" -type f)
    
    if [ -z "$COVERAGE_FILES" ]; then
        echo_colored "$RED" "❌ No coverage files found!"
        echo "Expected location: $COVERAGE_DIR/**/coverage.cobertura.xml"
        exit 1
    fi
    
    echo_colored "$GREEN" "Found coverage files:"
    echo "$COVERAGE_FILES"
    
    # Generate comprehensive reports
    dotnet tool run reportgenerator \
        -reports:"$COVERAGE_DIR/**/coverage.cobertura.xml" \
        -targetdir:"$REPORT_DIR" \
        -reporttypes:"Html;HtmlSummary;Badges;TextSummary;Cobertura;JsonSummary;MarkdownSummary" \
        -assemblyfilters:"+ConduitLLM.*;-*.Tests*;-*Test*" \
        -classfilters:"-*.Migrations*;-*.Program;-*.Startup" \
        -filefilters:"-**/Migrations/**;-**/Program.cs;-**/Startup.cs" \
        -verbosity:Info \
        -title:"Conduit LLM Coverage Report" \
        -tag:"$(date '+%Y-%m-%d %H:%M:%S')" \
        -historydir:"$REPORT_DIR/history"
    
    echo_colored "$GREEN" "✅ Reports generated in $REPORT_DIR"
}

# Function to display coverage summary
show_summary() {
    echo_colored "$BLUE" "📈 Coverage Summary"
    echo "==================="
    
    if [ ! -f "$REPORT_DIR/Summary.json" ]; then
        echo_colored "$RED" "❌ Summary file not found!"
        return 1
    fi
    
    # Parse coverage data
    LINE_COVERAGE=$(jq -r '.summary.linecoverage' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "0")
    BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "0")
    METHOD_COVERAGE=$(jq -r '.summary.methodcoverage' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "0")
    
    # Display overall coverage
    echo ""
    echo_colored "$BLUE" "Overall Coverage:"
    printf "  Line Coverage:   %s%%\n" "$LINE_COVERAGE"
    printf "  Branch Coverage: %s%%\n" "$BRANCH_COVERAGE"
    printf "  Method Coverage: %s%%\n" "$METHOD_COVERAGE"
    echo ""
    
    # Coverage assessment
    if (( $(echo "$LINE_COVERAGE >= 80" | bc -l) )); then
        echo_colored "$GREEN" "🟢 Excellent coverage! (≥80%)"
    elif (( $(echo "$LINE_COVERAGE >= 60" | bc -l) )); then
        echo_colored "$YELLOW" "🟡 Good coverage (60-79%)"
    elif (( $(echo "$LINE_COVERAGE >= 40" | bc -l) )); then
        echo_colored "$YELLOW" "🟠 Moderate coverage (40-59%)"
    else
        echo_colored "$RED" "🔴 Low coverage (<40%)"
        echo_colored "$RED" "Consider adding more tests!"
    fi
    
    echo ""
    echo_colored "$BLUE" "Coverage by Project:"
    echo "===================="
    
    # Project-specific coverage
    jq -r '.coverage[] | select(.name | contains("ConduitLLM")) | "  \(.name): \(.linecoverage)%"' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "  Coverage details unavailable"
    
    echo ""
    echo_colored "$BLUE" "Critical Services Analysis:"
    echo "==========================="
    
    # Analyze critical services
    CORE_COVERAGE=$(jq -r '.coverage[] | select(.name | contains("ConduitLLM.Core")) | .linecoverage' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "0")
    HTTP_COVERAGE=$(jq -r '.coverage[] | select(.name | contains("ConduitLLM.Http")) | .linecoverage' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "0")
    ADMIN_COVERAGE=$(jq -r '.coverage[] | select(.name | contains("ConduitLLM.Admin")) | .linecoverage' "$REPORT_DIR/Summary.json" 2>/dev/null || echo "0")
    
    assess_critical_service() {
        local name=$1
        local coverage=$2
        local threshold=80
        
        printf "  %-20s %s%%" "$name:" "$coverage"
        if (( $(echo "$coverage >= $threshold" | bc -l) )); then
            echo_colored "$GREEN" " ✅"
        else
            echo_colored "$RED" " ❌ (Target: ${threshold}%)"
        fi
    }
    
    assess_critical_service "Core Services" "$CORE_COVERAGE"
    assess_critical_service "HTTP API" "$HTTP_COVERAGE"
    assess_critical_service "Admin API" "$ADMIN_COVERAGE"
}

# Function to open reports
open_reports() {
    echo ""
    echo_colored "$BLUE" "📋 Available Reports:"
    echo "  HTML Report:     $REPORT_DIR/index.html"
    echo "  Text Summary:    $REPORT_DIR/Summary.txt"
    echo "  JSON Summary:    $REPORT_DIR/Summary.json"
    echo "  Badges:          $REPORT_DIR/badge_linecoverage.svg"
    
    # Try to open HTML report
    if command -v xdg-open >/dev/null 2>&1; then
        echo ""
        echo_colored "$GREEN" "🌐 Opening HTML report..."
        xdg-open "$REPORT_DIR/index.html" 2>/dev/null &
    elif command -v open >/dev/null 2>&1; then
        echo ""
        echo_colored "$GREEN" "🌐 Opening HTML report..."
        open "$REPORT_DIR/index.html" 2>/dev/null &
    fi
}

# Main execution
main() {
    case "${1:-}" in
        "run")
            run_coverage
            generate_reports
            show_summary
            open_reports
            ;;
        "report")
            if [ -d "$COVERAGE_DIR" ]; then
                generate_reports
                show_summary
                open_reports
            else
                echo_colored "$RED" "❌ No coverage data found. Run with 'run' first."
                exit 1
            fi
            ;;
        "summary")
            if [ -f "$REPORT_DIR/Summary.json" ]; then
                show_summary
            else
                echo_colored "$RED" "❌ No coverage summary found. Run coverage first."
                exit 1
            fi
            ;;
        *)
            echo "Usage: $0 {run|report|summary}"
            echo ""
            echo "Commands:"
            echo "  run     - Run tests with coverage and generate reports"
            echo "  report  - Generate reports from existing coverage data"
            echo "  summary - Display coverage summary from existing reports"
            echo ""
            echo "Examples:"
            echo "  $0 run      # Full coverage analysis"
            echo "  $0 summary  # Quick coverage check"
            exit 1
            ;;
    esac
}

main "$@"