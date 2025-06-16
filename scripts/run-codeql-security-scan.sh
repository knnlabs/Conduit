#!/bin/bash

# CodeQL Local Security Scan Script
# This script helps you run CodeQL security analysis locally

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
CODEQL_HOME="$PROJECT_ROOT/.codeql"
CODEQL_CLI="$CODEQL_HOME/codeql/codeql"

echo "=== CodeQL Local Security Scanner ==="
echo

# Function to download and install CodeQL
install_codeql() {
    echo "📦 Installing CodeQL CLI..."
    
    # Detect OS and architecture
    OS=$(uname -s | tr '[:upper:]' '[:lower:]')
    ARCH=$(uname -m)
    
    if [[ "$OS" == "darwin" ]]; then
        PLATFORM="osx"
    elif [[ "$OS" == "linux" ]]; then
        PLATFORM="linux"
    else
        echo "❌ Unsupported OS: $OS"
        exit 1
    fi
    
    if [[ "$ARCH" == "x86_64" ]]; then
        ARCH="64"
    elif [[ "$ARCH" == "aarch64" || "$ARCH" == "arm64" ]]; then
        ARCH="arm64"
    else
        echo "❌ Unsupported architecture: $ARCH"
        exit 1
    fi
    
    # Download URL for latest CodeQL
    CODEQL_URL="https://github.com/github/codeql-action/releases/latest/download/codeql-bundle-${PLATFORM}${ARCH}.tar.gz"
    
    mkdir -p "$CODEQL_HOME"
    cd "$CODEQL_HOME"
    
    echo "📥 Downloading CodeQL from $CODEQL_URL..."
    curl -L "$CODEQL_URL" -o codeql-bundle.tar.gz
    
    echo "📦 Extracting CodeQL..."
    tar -xzf codeql-bundle.tar.gz
    rm codeql-bundle.tar.gz
    
    echo "✅ CodeQL installed successfully!"
}

# Check if CodeQL is installed
if [[ ! -f "$CODEQL_CLI" ]]; then
    echo "CodeQL CLI not found at $CODEQL_CLI"
    read -p "Would you like to download and install CodeQL? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        install_codeql
    else
        echo "❌ CodeQL is required to run security scans. Exiting."
        exit 1
    fi
fi

cd "$PROJECT_ROOT"

# Create database directory
DB_DIR="$PROJECT_ROOT/.codeql-db"
RESULTS_DIR="$PROJECT_ROOT/.codeql-results"
mkdir -p "$RESULTS_DIR"

# Function to create CodeQL database
create_database() {
    echo "🔨 Creating CodeQL database for C# code..."
    
    # Remove existing database
    rm -rf "$DB_DIR"
    
    # Create new database
    "$CODEQL_CLI" database create "$DB_DIR" \
        --language=csharp \
        --command="dotnet build /t:rebuild" \
        --overwrite
    
    echo "✅ Database created successfully!"
}

# Function to run security queries
run_security_scan() {
    echo "🔍 Running security analysis..."
    
    # Run C# security queries
    "$CODEQL_CLI" database analyze "$DB_DIR" \
        --format=sarif-latest \
        --output="$RESULTS_DIR/security-results.sarif" \
        --download \
        csharp-security-and-quality.qls
    
    echo "✅ Security analysis complete!"
}

# Function to generate readable report
generate_report() {
    echo "📊 Generating readable report..."
    
    # Convert SARIF to CSV for easier reading
    "$CODEQL_CLI" database interpret-results "$DB_DIR" \
        --format=csv \
        --output="$RESULTS_DIR/security-results.csv" \
        "$RESULTS_DIR/security-results.sarif"
    
    # Also create a text summary
    echo "=== CodeQL Security Scan Results ===" > "$RESULTS_DIR/summary.txt"
    echo "Scan Date: $(date)" >> "$RESULTS_DIR/summary.txt"
    echo "" >> "$RESULTS_DIR/summary.txt"
    
    # Parse SARIF with jq if available
    if command -v jq &> /dev/null; then
        echo "Found $(jq '.runs[0].results | length' "$RESULTS_DIR/security-results.sarif") security alerts" >> "$RESULTS_DIR/summary.txt"
        echo "" >> "$RESULTS_DIR/summary.txt"
        echo "Top Issues:" >> "$RESULTS_DIR/summary.txt"
        jq -r '.runs[0].results | group_by(.ruleId) | map({rule: .[0].ruleId, count: length}) | sort_by(.count) | reverse | .[] | "\(.count) x \(.rule)"' "$RESULTS_DIR/security-results.sarif" >> "$RESULTS_DIR/summary.txt"
    else
        echo "Install 'jq' for better result parsing" >> "$RESULTS_DIR/summary.txt"
    fi
    
    echo "✅ Reports generated in $RESULTS_DIR/"
}

# Function to run quick scan for specific vulnerability
scan_log_injection() {
    echo "🎯 Running targeted scan for log injection vulnerabilities..."
    
    # Create a custom query for log injection
    cat > "$RESULTS_DIR/log-injection.ql" << 'EOF'
/**
 * @name Log injection
 * @description Finds log statements that include user input
 * @kind problem
 * @problem.severity error
 * @id cs/log-injection
 * @tags security
 */

import csharp
import semmle.code.csharp.security.dataflow.LogInjection

from LogInjection::Sink sink, DataFlow::Node source
where LogInjection::flow(source, sink)
select sink, "This log entry depends on a $@.", source, "user-provided value"
EOF

    "$CODEQL_CLI" query run \
        --database="$DB_DIR" \
        --output="$RESULTS_DIR/log-injection-results.bqrs" \
        "$RESULTS_DIR/log-injection.ql"
    
    "$CODEQL_CLI" bqrs decode \
        --format=csv \
        --output="$RESULTS_DIR/log-injection-results.csv" \
        "$RESULTS_DIR/log-injection-results.bqrs"
    
    echo "✅ Log injection scan complete!"
    echo "Results saved to: $RESULTS_DIR/log-injection-results.csv"
}

# Main menu
echo "What would you like to do?"
echo "1) Full security scan (slower, comprehensive)"
echo "2) Quick log injection scan (faster, targeted)"
echo "3) Create/update database only"
echo "4) Run analysis on existing database"
echo "5) Generate report from existing results"

read -p "Enter your choice (1-5): " choice

case $choice in
    1)
        create_database
        run_security_scan
        generate_report
        ;;
    2)
        if [[ ! -d "$DB_DIR" ]]; then
            echo "❌ Database not found. Creating it first..."
            create_database
        fi
        scan_log_injection
        ;;
    3)
        create_database
        ;;
    4)
        if [[ ! -d "$DB_DIR" ]]; then
            echo "❌ Database not found. Please create it first (option 3)."
            exit 1
        fi
        run_security_scan
        generate_report
        ;;
    5)
        if [[ ! -f "$RESULTS_DIR/security-results.sarif" ]]; then
            echo "❌ No results found. Please run analysis first."
            exit 1
        fi
        generate_report
        ;;
    *)
        echo "❌ Invalid choice"
        exit 1
        ;;
esac

echo
echo "🎉 Done! Check the results in: $RESULTS_DIR/"
echo
echo "Key files:"
echo "  - $RESULTS_DIR/security-results.sarif (full SARIF report)"
echo "  - $RESULTS_DIR/security-results.csv (CSV format)"
echo "  - $RESULTS_DIR/summary.txt (text summary)"
if [[ -f "$RESULTS_DIR/log-injection-results.csv" ]]; then
    echo "  - $RESULTS_DIR/log-injection-results.csv (log injection specific)"
fi