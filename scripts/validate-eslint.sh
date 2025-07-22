#!/bin/bash

# Script to validate ESLint configurations before pushing
# This prevents CI/CD failures due to ESLint config issues

set -e

echo "🔍 Validating ESLint configurations..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track if any validation fails
FAILED=0

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
    
    # Run ESLint
    cd "$dir"
    if npm run lint >/dev/null 2>&1; then
        echo -e "${GREEN}✅ ESLint validation passed${NC}"
    else
        echo -e "${RED}❌ ESLint validation failed!${NC}"
        echo "   Running ESLint to show errors:"
        npm run lint || true
        FAILED=1
    fi
    cd - >/dev/null
}

# Validate all TypeScript projects
validate_eslint "SDKs/Node/Admin" "Admin Client"
validate_eslint "SDKs/Node/Core" "Core Client"
validate_eslint "ConduitLLM.WebUI" "WebUI"

echo -e "\n📊 Summary:"
if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✅ All ESLint configurations are valid!${NC}"
    exit 0
else
    echo -e "${RED}❌ ESLint validation failed!${NC}"
    echo -e "${YELLOW}⚠️  Fix the issues above before pushing to avoid CI/CD failures${NC}"
    exit 1
fi