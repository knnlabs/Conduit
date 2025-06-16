#!/bin/bash

# CodeQL Local Analysis Script for Conduit
# This script runs CodeQL security analysis on the entire .NET solution

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
CODEQL_PATH="/home/nbn/Code/Conduit/.codeql/codeql/codeql"
DB_NAME="conduit-codeql-db"
RESULTS_DIR="codeql-results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

echo -e "${GREEN}CodeQL Security Analysis for Conduit${NC}"
echo "======================================="

# Check if CodeQL exists
if [ ! -f "$CODEQL_PATH" ]; then
    echo -e "${RED}Error: CodeQL not found at $CODEQL_PATH${NC}"
    exit 1
fi

# Clean up old database if exists
if [ -d "$DB_NAME" ]; then
    echo -e "${YELLOW}Removing existing CodeQL database...${NC}"
    rm -rf "$DB_NAME"
fi

# Create results directory
mkdir -p "$RESULTS_DIR"

# Step 1: Create CodeQL database
echo -e "\n${GREEN}Step 1: Creating CodeQL database...${NC}"
echo "This will build the entire solution and may take several minutes..."
$CODEQL_PATH database create "$DB_NAME" --language=csharp --overwrite

# Step 2: Run security analysis
echo -e "\n${GREEN}Step 2: Running security analysis...${NC}"

# Run extended security suite (same as GitHub)
echo "Running extended security queries..."
$CODEQL_PATH database analyze "$DB_NAME" \
    --format=sarif-latest \
    --output="$RESULTS_DIR/security-extended-$TIMESTAMP.sarif" \
    csharp-security-extended

# Also run just the log injection query separately for focused analysis
echo -e "\n${GREEN}Running log injection query specifically...${NC}"
$CODEQL_PATH database analyze "$DB_NAME" \
    --format=sarif-latest \
    --output="$RESULTS_DIR/log-injection-$TIMESTAMP.sarif" \
    csharp-security-and-quality \
    --sarif-category=log-injection

# Step 3: Generate summary
echo -e "\n${GREEN}Step 3: Analysis Summary${NC}"
echo "========================"

# Count total security alerts
if command -v jq &> /dev/null; then
    TOTAL_ALERTS=$(jq '.runs[0].results | length' "$RESULTS_DIR/security-extended-$TIMESTAMP.sarif")
    LOG_ALERTS=$(jq '[.runs[0].results[] | select(.ruleId == "cs/log-injection" or .ruleId == "cs/log-forging")] | length' "$RESULTS_DIR/security-extended-$TIMESTAMP.sarif")
    
    echo -e "Total security alerts: ${YELLOW}$TOTAL_ALERTS${NC}"
    echo -e "Log injection alerts: ${YELLOW}$LOG_ALERTS${NC}"
    
    # Show top 5 alert types
    echo -e "\nTop alert types:"
    jq -r '.runs[0].results | group_by(.ruleId) | map({rule: .[0].ruleId, count: length}) | sort_by(-.count) | .[:5] | .[] | "\(.count)\t\(.rule)"' \
        "$RESULTS_DIR/security-extended-$TIMESTAMP.sarif"
else
    echo -e "${YELLOW}Note: Install 'jq' for detailed summary (sudo apt-get install jq)${NC}"
fi

echo -e "\n${GREEN}Analysis complete!${NC}"
echo "Results saved to:"
echo "  - $RESULTS_DIR/security-extended-$TIMESTAMP.sarif"
echo "  - $RESULTS_DIR/log-injection-$TIMESTAMP.sarif"

# Optional: Generate CSV format for easier viewing
echo -e "\n${GREEN}Generating CSV report...${NC}"
$CODEQL_PATH database analyze "$DB_NAME" \
    --format=csv \
    --output="$RESULTS_DIR/security-report-$TIMESTAMP.csv" \
    csharp-security-extended

echo -e "\nCSV report: $RESULTS_DIR/security-report-$TIMESTAMP.csv"

# Cleanup option
echo -e "\n${YELLOW}Keep CodeQL database for future queries? (y/n)${NC}"
read -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    rm -rf "$DB_NAME"
    echo "Database removed."
else
    echo "Database kept at: $DB_NAME"
fi