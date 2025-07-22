#!/bin/bash

# Strict ESLint validation - fails on ANY errors (not warnings)
# This is what CI/CD uses

set -e

echo "🔍 Running STRICT ESLint validation (CI/CD mode)..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track if any validation fails
FAILED=0
TOTAL_ERRORS=0
TOTAL_WARNINGS=0

# Function to validate ESLint in a directory
validate_eslint() {
    local dir=$1
    local name=$2
    
    echo -e "\n📁 Checking $name ($dir)..."
    
    if [ ! -f "$dir/package.json" ]; then
        echo -e "${YELLOW}⚠️  No package.json found, skipping${NC}"
        return
    fi
    
    # Check if ESLint is configured
    if ! grep -q "eslint" "$dir/package.json"; then
        echo -e "${YELLOW}⚠️  No ESLint configured, skipping${NC}"
        return
    fi
    
    # Check for conflicting config files
    if [ -f "$dir/.eslintrc.js" ] && [ -f "$dir/eslint.config.js" ]; then
        echo -e "${RED}❌ ERROR: Both .eslintrc.js and eslint.config.js exist!${NC}"
        echo "   Remove the old .eslintrc.js file"
        FAILED=1
        return
    fi
    
    if [ -f "$dir/.eslintrc.json" ] && [ -f "$dir/eslint.config.js" ]; then
        echo -e "${RED}❌ ERROR: Both .eslintrc.json and eslint.config.js exist!${NC}"
        echo "   Remove the old .eslintrc.json file"
        FAILED=1
        return
    fi
    
    # Run ESLint and capture output
    cd "$dir"
    LINT_OUTPUT=$(npm run lint 2>&1 || true)
    
    # Parse the output for errors and warnings
    if echo "$LINT_OUTPUT" | grep -q "✖.*error"; then
        # Extract error count
        ERROR_COUNT=$(echo "$LINT_OUTPUT" | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
        TOTAL_ERRORS=$((TOTAL_ERRORS + ERROR_COUNT))
        echo -e "${RED}❌ Found $ERROR_COUNT error(s)${NC}"
        FAILED=1
        
        # Show the errors
        echo "$LINT_OUTPUT" | grep "error" | head -10
        echo "   ..."
    else
        echo -e "${GREEN}✅ No errors found${NC}"
    fi
    
    if echo "$LINT_OUTPUT" | grep -q "⚠.*warning"; then
        WARNING_COUNT=$(echo "$LINT_OUTPUT" | grep -oE "[0-9]+ warning" | grep -oE "[0-9]+" | head -1)
        TOTAL_WARNINGS=$((TOTAL_WARNINGS + WARNING_COUNT))
        echo -e "${YELLOW}⚠️  Found $WARNING_COUNT warning(s) (non-blocking)${NC}"
    fi
    
    cd - >/dev/null
}

# Validate all TypeScript projects
validate_eslint "SDKs/Node/Admin" "Admin Client"
validate_eslint "SDKs/Node/Core" "Core Client"
validate_eslint "ConduitLLM.WebUI" "WebUI"

echo -e "\n📊 Summary:"
echo -e "Total Errors: ${TOTAL_ERRORS}"
echo -e "Total Warnings: ${TOTAL_WARNINGS}"

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✅ All ESLint validations passed! (No errors)${NC}"
    if [ $TOTAL_WARNINGS -gt 0 ]; then
        echo -e "${YELLOW}⚠️  Consider fixing the $TOTAL_WARNINGS warning(s) for better code quality${NC}"
    fi
    exit 0
else
    echo -e "${RED}❌ ESLint validation FAILED!${NC}"
    echo -e "${RED}❌ Found $TOTAL_ERRORS error(s) that MUST be fixed${NC}"
    echo -e ""
    echo -e "This is the same check that runs in CI/CD."
    echo -e "Your push/build WILL FAIL if you don't fix these errors."
    echo -e ""
    echo -e "To fix:"
    echo -e "1. Run './scripts/fix-lint-errors.sh' to auto-fix what's possible"
    echo -e "2. Manually fix any remaining errors"
    echo -e "3. Re-run this script to verify"
    exit 1
fi