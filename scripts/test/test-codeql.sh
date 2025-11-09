#!/bin/bash

# CodeQL Local Testing Script
# Tests CodeQL analysis locally to verify error counts before pushing to GitHub

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
CODEQL_DIR="$PROJECT_ROOT/.codeql"
CODEQL_DB="$PROJECT_ROOT/codeql-db"
RESULTS_DIR="$PROJECT_ROOT/codeql-results"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== CodeQL Local Testing Script ===${NC}"
echo ""

# Function to download and install CodeQL
install_codeql() {
    echo -e "${YELLOW}Installing CodeQL CLI...${NC}"
    
    # Get latest CodeQL bundle version from GitHub
    echo "Fetching latest CodeQL version..."
    LATEST_VERSION=$(curl -s https://api.github.com/repos/github/codeql-action/releases/latest | grep '"tag_name"' | sed -E 's/.*"([^"]+)".*/\1/')
    
    if [ -z "$LATEST_VERSION" ]; then
        echo -e "${RED}Failed to fetch latest version, using fallback${NC}"
        LATEST_VERSION="codeql-bundle-v2.20.5"
    fi
    
    echo "Latest version: $LATEST_VERSION"
    
    # Download CodeQL bundle
    DOWNLOAD_URL="https://github.com/github/codeql-action/releases/download/${LATEST_VERSION}/codeql-bundle-linux64.tar.gz"
    echo "Downloading from: $DOWNLOAD_URL"
    
    mkdir -p "$CODEQL_DIR"
    cd "$CODEQL_DIR"
    
    if ! curl -L -o codeql-bundle.tar.gz "$DOWNLOAD_URL"; then
        echo -e "${RED}Failed to download CodeQL bundle${NC}"
        exit 1
    fi
    
    echo "Extracting CodeQL bundle..."
    tar xzf codeql-bundle.tar.gz
    rm codeql-bundle.tar.gz
    
    echo -e "${GREEN}CodeQL installed successfully${NC}"
}

# Check if CodeQL is installed
if [ ! -d "$CODEQL_DIR/codeql" ]; then
    echo -e "${YELLOW}CodeQL not found at $CODEQL_DIR${NC}"
    install_codeql
else
    echo -e "${GREEN}CodeQL found at $CODEQL_DIR${NC}"
    # Check version
    "$CODEQL_DIR/codeql/codeql" version
fi

# Add CodeQL to PATH
export PATH="$CODEQL_DIR/codeql:$PATH"

cd "$PROJECT_ROOT"

# Parse command line arguments
QUICK_MODE=false
CLEAN_BUILD=false
FILTER_MODE=true

while [[ $# -gt 0 ]]; do
    case $1 in
        --quick)
            QUICK_MODE=true
            shift
            ;;
        --clean)
            CLEAN_BUILD=true
            shift
            ;;
        --no-filter)
            FILTER_MODE=false
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --quick       Run minimal analysis (faster, less comprehensive)"
            echo "  --clean       Force rebuild of CodeQL database"
            echo "  --no-filter   Don't apply workflow query filters"
            echo "  --help        Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Clean old database if requested or doesn't exist
if [ "$CLEAN_BUILD" = true ] || [ ! -d "$CODEQL_DB" ]; then
    echo -e "${YELLOW}Creating CodeQL database (this will take 5-10 minutes)...${NC}"
    rm -rf "$CODEQL_DB"
    
    # Create database with build tracing
    # Note: CodeQL requires a script or single command, not shell syntax
    # Create a temporary build script
    BUILD_SCRIPT="$PROJECT_ROOT/.codeql-build.sh"
    cat > "$BUILD_SCRIPT" << 'EOF'
#!/bin/bash
set -e
dotnet clean --configuration Release
dotnet build --configuration Release
EOF
    chmod +x "$BUILD_SCRIPT"
    
    codeql database create "$CODEQL_DB" \
        --language=csharp \
        --source-root="$PROJECT_ROOT" \
        --command="$BUILD_SCRIPT" \
        --overwrite
    
    # Clean up build script
    rm -f "$BUILD_SCRIPT"
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Failed to create CodeQL database${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}Database created successfully${NC}"
else
    echo -e "${GREEN}Using existing database at $CODEQL_DB${NC}"
fi

# Create results directory
mkdir -p "$RESULTS_DIR"

# Prepare query suite
if [ "$QUICK_MODE" = true ]; then
    QUERY_SUITE="csharp-security-extended.qls"
    echo -e "${YELLOW}Running in quick mode (security queries only)${NC}"
else
    QUERY_SUITE="csharp-security-and-quality.qls"
    echo -e "${YELLOW}Running full analysis (security and quality)${NC}"
fi

# Create config file with filters if needed
if [ "$FILTER_MODE" = true ]; then
    echo -e "${BLUE}Applying workflow query filters...${NC}"
    cat > "$RESULTS_DIR/qlconfig.yml" << 'EOF'
query-filters:
  - exclude:
      id: js/unused-local-variable
  - exclude:
      id: cs/static-field-written-by-instance
  - exclude:
      id: cs/loss-of-precision
      tags: test
  - exclude:
      id: cs/unused-collection
      tags: test
EOF
    CONFIG_ARG="--sarif-category=/language:csharp"
else
    CONFIG_ARG="--sarif-category=/language:csharp"
fi

# Run analysis
echo -e "${YELLOW}Running CodeQL analysis (this will take 10-15 minutes)...${NC}"
echo ""

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SARIF_FILE="$RESULTS_DIR/results_${TIMESTAMP}.sarif"

codeql database analyze "$CODEQL_DB" \
    --format=sarif-latest \
    --output="$SARIF_FILE" \
    $CONFIG_ARG \
    --sarif-add-query-help \
    "$QUERY_SUITE"

if [ $? -ne 0 ]; then
    echo -e "${RED}CodeQL analysis failed${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}=== Analysis Complete ===${NC}"
echo ""

# Generate summary
echo -e "${BLUE}=== Results Summary ===${NC}"
echo ""

# Count total issues
TOTAL_ISSUES=$(grep -o '"ruleId"' "$SARIF_FILE" 2>/dev/null | wc -l || echo "0")
echo -e "Total issues found: ${YELLOW}$TOTAL_ISSUES${NC}"
echo ""

# Show breakdown by severity if jq is available
if command -v jq &> /dev/null; then
    echo -e "${BLUE}Issues by severity:${NC}"
    jq -r '.runs[0].results[] | 
        if .rule.properties.security_severity then 
            "security:" + .rule.properties.security_severity 
        else 
            .level // "warning" 
        end' "$SARIF_FILE" 2>/dev/null | sort | uniq -c | sort -rn || true
    
    echo ""
    echo -e "${BLUE}Top 20 issue types:${NC}"
    jq -r '.runs[0].results[] | .ruleId' "$SARIF_FILE" 2>/dev/null | sort | uniq -c | sort -rn | head -20 || true
    
    echo ""
    echo -e "${BLUE}Error-level issues:${NC}"
    jq -r '.runs[0].results[] | select(.level == "error") | .ruleId' "$SARIF_FILE" 2>/dev/null | sort | uniq -c | sort -rn || echo "No error-level issues found"
else
    echo -e "${YELLOW}Install jq for detailed breakdown: sudo apt-get install jq${NC}"
fi

echo ""
echo -e "${GREEN}Results saved to: $SARIF_FILE${NC}"
echo ""

# Create a simple HTML report if possible
if command -v python3 &> /dev/null; then
    echo -e "${BLUE}Generating HTML summary...${NC}"
    HTML_FILE="$RESULTS_DIR/summary_${TIMESTAMP}.html"
    
    python3 -c "
import json
import html

with open('$SARIF_FILE', 'r') as f:
    data = json.load(f)

results = data['runs'][0]['results']
total = len(results)

# Count by rule
rule_counts = {}
for result in results:
    rule_id = result.get('ruleId', 'unknown')
    rule_counts[rule_id] = rule_counts.get(rule_id, 0) + 1

# Sort by count
sorted_rules = sorted(rule_counts.items(), key=lambda x: x[1], reverse=True)

html_content = '''<!DOCTYPE html>
<html>
<head>
    <title>CodeQL Results Summary</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #333; }
        table { border-collapse: collapse; width: 100%; max-width: 800px; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #4CAF50; color: white; }
        tr:nth-child(even) { background-color: #f2f2f2; }
        .summary { background-color: #e7f3fe; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
    </style>
</head>
<body>
    <h1>CodeQL Analysis Results</h1>
    <div class=\"summary\">
        <h2>Summary</h2>
        <p><strong>Total Issues:</strong> ''' + str(total) + '''</p>
        <p><strong>Unique Rule Types:</strong> ''' + str(len(rule_counts)) + '''</p>
        <p><strong>Analysis Date:</strong> $TIMESTAMP</p>
    </div>
    <h2>Issues by Type</h2>
    <table>
        <tr><th>Rule ID</th><th>Count</th><th>Percentage</th></tr>'''

for rule_id, count in sorted_rules[:50]:  # Top 50
    percentage = (count / total * 100) if total > 0 else 0
    html_content += f'''
        <tr>
            <td>{html.escape(rule_id)}</td>
            <td>{count}</td>
            <td>{percentage:.1f}%</td>
        </tr>'''

html_content += '''
    </table>
</body>
</html>'''

with open('$HTML_FILE', 'w') as f:
    f.write(html_content)

print(f'HTML summary saved to: $HTML_FILE')
" || true
fi

echo ""
echo -e "${GREEN}=== Analysis Complete ===${NC}"
echo ""
echo "Compare with GitHub's count by checking:"
echo "https://github.com/knnlabs/Conduit/security/code-scanning"
echo ""

# Show comparison with last run if available
LAST_SARIF=$(ls -t "$RESULTS_DIR"/results_*.sarif 2>/dev/null | sed -n '2p')
if [ -n "$LAST_SARIF" ] && [ "$LAST_SARIF" != "$SARIF_FILE" ]; then
    LAST_COUNT=$(grep -o '"ruleId"' "$LAST_SARIF" 2>/dev/null | wc -l || echo "0")
    DIFF=$((TOTAL_ISSUES - LAST_COUNT))
    
    echo -e "${BLUE}Comparison with last run:${NC}"
    echo "Previous: $LAST_COUNT issues"
    echo "Current:  $TOTAL_ISSUES issues"
    
    if [ $DIFF -gt 0 ]; then
        echo -e "Change:   ${RED}+$DIFF issues${NC}"
    elif [ $DIFF -lt 0 ]; then
        echo -e "Change:   ${GREEN}$DIFF issues${NC}"
    else
        echo -e "Change:   ${YELLOW}No change${NC}"
    fi
fi