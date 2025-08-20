#!/bin/bash

# GitHub Actions Workflow Validation Script
# This script validates all GitHub Actions workflow files locally
# to catch issues before they fail in GitHub

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKFLOWS_DIR="$PROJECT_ROOT/.github/workflows"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Tracking variables
TOTAL_ERRORS=0
TOTAL_WARNINGS=0
WORKFLOWS_CHECKED=0

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

print_header() {
    echo ""
    print_status "$BLUE" "=============================================="
    print_status "$BLUE" "$1"
    print_status "$BLUE" "=============================================="
}

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to validate YAML syntax
validate_yaml_syntax() {
    local file=$1
    local filename=$(basename "$file")
    
    print_status "$YELLOW" "Checking $filename..."
    
    # Check if file exists
    if [ ! -f "$file" ]; then
        print_status "$RED" "  ❌ File not found: $file"
        ((TOTAL_ERRORS++))
        return 1
    fi
    
    # Check YAML syntax if yq is available
    if command_exists yq; then
        if ! yq eval '.' "$file" > /dev/null 2>&1; then
            print_status "$RED" "  ❌ Invalid YAML syntax in $filename"
            ((TOTAL_ERRORS++))
            return 1
        fi
    else
        # Basic YAML validation without yq
        if ! grep -q "^name:" "$file"; then
            print_status "$RED" "  ❌ Missing 'name' field in $filename"
            ((TOTAL_ERRORS++))
        fi
        if ! grep -q "^on:" "$file"; then
            print_status "$RED" "  ❌ Missing 'on' trigger in $filename"
            ((TOTAL_ERRORS++))
        fi
    fi
    
    print_status "$GREEN" "  ✓ Valid YAML syntax"
    return 0
}

# Function to check for common path issues
check_path_references() {
    local file=$1
    local filename=$(basename "$file")
    local found_issues=false
    
    # Check for old client paths (should be SDKs)
    if grep -E "NodeClients/|Clients/Node/" "$file" > /dev/null 2>&1; then
        print_status "$RED" "  ❌ Found outdated client paths (should be SDKs/Node/*):"
        grep -n -E "NodeClients/|Clients/Node/" "$file" | head -3
        ((TOTAL_ERRORS++))
        found_issues=true
    fi
    
    # Check for correct SDK paths
    if grep -E "SDKs/Node/(Admin|Core|Common)" "$file" > /dev/null 2>&1; then
        print_status "$GREEN" "  ✓ Using correct SDK paths"
    fi
    
    # Check for non-existent directories (simplified to avoid hangs)
    local paths_to_check=$(grep -E '(path:|cache-dependency-path:|working-directory:)' "$file" | grep -v '^\s*#' | cut -d: -f2- | tr -d ' ' | grep -v '^\s*$' | head -20)
    
    for path in $paths_to_check; do
        # Skip variables, wildcards, and home directory references
        if [[ "$path" =~ ^\$\{ ]] || [[ "$path" =~ \* ]] || [[ "$path" =~ ^~ ]]; then
            continue
        fi
        
        # Skip single words (likely not paths)
        if [[ ! "$path" =~ / ]]; then
            continue
        fi
        
        # Check if it looks like a relative path
        if [[ "$path" =~ ^\./(.+) ]] || [[ "$path" =~ ^([A-Za-z][^/]*/.*) ]]; then
            test_path="${BASH_REMATCH[1]}"
            full_path="$PROJECT_ROOT/$test_path"
            
            # Only warn for paths that look like they should exist
            if [[ "$test_path" =~ \.(yml|yaml|json|sh|ps1)$ ]] || [[ "$test_path" =~ ^(scripts|SDKs|website)/ ]]; then
                if [ ! -e "$full_path" ]; then
                    print_status "$YELLOW" "  ⚠️ Missing file: $test_path"
                    ((TOTAL_WARNINGS++))
                fi
            fi
        fi
    done
    
    if [ "$found_issues" = false ] && [ $TOTAL_WARNINGS -eq 0 ]; then
        print_status "$GREEN" "  ✓ Path references look good"
    fi
}

# Function to check for runner issues
check_runners() {
    local file=$1
    local filename=$(basename "$file")
    
    # Check for non-existent runners (exclude comments)
    if grep -E "^\s*runs-on:.*arm|^\s*runs-on:.*ubuntu-.*-arm" "$file" | grep -v '^\s*#' > /dev/null 2>&1; then
        print_status "$RED" "  ❌ Found ARM64 runner (not supported by GitHub Actions):"
        grep -n -E "^\s*runs-on:.*arm|^\s*runs-on:.*ubuntu-.*-arm" "$file" | grep -v '^\s*#'
        ((TOTAL_ERRORS++))
    fi
    
    # Check for valid runners
    if grep -E "runs-on:\s*(ubuntu-latest|ubuntu-2[0-9]\.[0-9]{2}|windows-latest|macos-latest)" "$file" > /dev/null 2>&1; then
        print_status "$GREEN" "  ✓ Using valid GitHub-hosted runners"
    fi
}

# Function to check for secret references
check_secrets() {
    local file=$1
    local filename=$(basename "$file")
    local required_secrets=""
    
    # Extract secret references
    while IFS= read -r secret; do
        if [[ "$secret" =~ secrets\.([A-Z_]+) ]]; then
            secret_name="${BASH_REMATCH[1]}"
            if [ "$secret_name" != "GITHUB_TOKEN" ]; then
                required_secrets="$required_secrets $secret_name"
            fi
        fi
    done < <(grep -oE '\$\{\{\s*secrets\.[A-Z_]+\s*\}\}' "$file")
    
    if [ -n "$required_secrets" ]; then
        print_status "$YELLOW" "  ℹ️ Required secrets:$required_secrets"
        print_status "$YELLOW" "    Make sure these are configured in repository settings"
    fi
}

# Function to check for deprecated actions
check_deprecated_actions() {
    local file=$1
    local filename=$(basename "$file")
    
    # Check for old action versions
    if grep -E "actions/checkout@v[1-3]|actions/setup-node@v[1-3]" "$file" > /dev/null 2>&1; then
        print_status "$YELLOW" "  ⚠️ Using older action versions (consider updating to v4+):"
        grep -n -E "actions/checkout@v[1-3]|actions/setup-node@v[1-3]" "$file" | head -3
        ((TOTAL_WARNINGS++))
    fi
}

# Function to check for concurrency issues
check_concurrency() {
    local file=$1
    local filename=$(basename "$file")
    
    # Check for versioning/publishing workflows without proper concurrency control
    if grep -E "version|publish|release" "$file" > /dev/null 2>&1; then
        if ! grep -q "cancel-in-progress: false" "$file"; then
            print_status "$YELLOW" "  ⚠️ Version/publish workflow should have 'cancel-in-progress: false'"
            ((TOTAL_WARNINGS++))
        else
            print_status "$GREEN" "  ✓ Proper concurrency control for versioning"
        fi
    fi
}

# Function to validate Docker manifest creation
check_docker_manifests() {
    local file=$1
    local filename=$(basename "$file")
    
    if grep -q "docker-manifest" "$file" || grep -q "imagetools create" "$file"; then
        # Check for ARM64 references that should be removed
        if grep -E "arm64|linux/arm64" "$file" > /dev/null 2>&1; then
            print_status "$YELLOW" "  ⚠️ Found ARM64 references in Docker manifest (ARM64 builds removed):"
            grep -n -E "arm64|linux/arm64" "$file" | head -3
            ((TOTAL_WARNINGS++))
        fi
    fi
}

# Function to check for missing script references
check_script_references() {
    local file=$1
    local filename=$(basename "$file")
    
    # Extract script references
    while IFS= read -r script_path; do
        if [[ "$script_path" =~ \./scripts/(.+\.sh) ]] || [[ "$script_path" =~ scripts/(.+\.sh) ]]; then
            full_script_path="$PROJECT_ROOT/scripts/${BASH_REMATCH[1]}"
            if [ ! -f "$full_script_path" ]; then
                print_status "$YELLOW" "  ⚠️ Referenced script not found: scripts/${BASH_REMATCH[1]}"
                ((TOTAL_WARNINGS++))
            fi
        fi
    done < <(grep -oE '\./scripts/[^[:space:]]+\.sh|scripts/[^[:space:]]+\.sh' "$file")
}

# Main validation function
validate_workflow() {
    local file=$1
    
    validate_yaml_syntax "$file"
    check_path_references "$file"
    check_runners "$file"
    check_secrets "$file"
    check_deprecated_actions "$file"
    check_concurrency "$file"
    check_docker_manifests "$file"
    check_script_references "$file"
    
    ((WORKFLOWS_CHECKED++))
}

# Main execution
main() {
    print_header "GitHub Actions Workflow Validation"
    
    # Check if workflows directory exists
    if [ ! -d "$WORKFLOWS_DIR" ]; then
        print_status "$RED" "ERROR: Workflows directory not found at $WORKFLOWS_DIR"
        exit 1
    fi
    
    # Check for yq installation
    if ! command_exists yq; then
        print_status "$YELLOW" "Note: 'yq' not found. Install it for better YAML validation:"
        print_status "$YELLOW" "  brew install yq  # macOS"
        print_status "$YELLOW" "  sudo snap install yq  # Ubuntu"
        echo ""
    fi
    
    # Validate each workflow file
    print_header "Validating Workflow Files"
    
    for workflow in "$WORKFLOWS_DIR"/*.yml "$WORKFLOWS_DIR"/*.yaml; do
        if [ -f "$workflow" ]; then
            validate_workflow "$workflow"
            echo ""
        fi
    done
    
    # Summary
    print_header "Validation Summary"
    
    print_status "$BLUE" "Workflows checked: $WORKFLOWS_CHECKED"
    
    if [ $TOTAL_ERRORS -eq 0 ] && [ $TOTAL_WARNINGS -eq 0 ]; then
        print_status "$GREEN" "✅ All workflows validated successfully!"
        print_status "$GREEN" "No issues found. Safe to push to GitHub."
    else
        if [ $TOTAL_ERRORS -gt 0 ]; then
            print_status "$RED" "❌ Found $TOTAL_ERRORS error(s)"
            print_status "$RED" "These MUST be fixed before pushing to GitHub"
        fi
        if [ $TOTAL_WARNINGS -gt 0 ]; then
            print_status "$YELLOW" "⚠️ Found $TOTAL_WARNINGS warning(s)"
            print_status "$YELLOW" "Consider addressing these warnings"
        fi
    fi
    
    echo ""
    
    # Exit with error if any errors found
    if [ $TOTAL_ERRORS -gt 0 ]; then
        exit 1
    fi
    
    exit 0
}

# Run main function
main