#!/bin/bash

# Script to automatically fix lint errors where possible

set -e

echo "🔧 Attempting to fix lint errors..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track if any fixes were made
FIXED=0

# Function to fix lint errors in a directory
fix_lint() {
    local dir=$1
    local name=$2
    
    echo -e "\n📁 Fixing $name ($dir)..."
    
    if [ ! -f "$dir/package.json" ]; then
        echo -e "${YELLOW}⚠️  No package.json found, skipping${NC}"
        return
    fi
    
    # Check if ESLint is configured
    if ! grep -q "eslint" "$dir/package.json"; then
        echo -e "${YELLOW}⚠️  No ESLint configured, skipping${NC}"
        return
    fi
    
    cd "$dir"
    
    # Try to auto-fix
    if npm run lint -- --fix; then
        echo -e "${GREEN}✅ Auto-fix completed successfully${NC}"
        FIXED=1
    else
        echo -e "${YELLOW}⚠️  Some errors could not be auto-fixed${NC}"
        echo "   Manual fixes required. Running lint to show remaining errors:"
        npm run lint || true
    fi
    
    cd - >/dev/null
}

# Fix all TypeScript projects
fix_lint "Clients/Node/Admin" "Admin Client"
fix_lint "Clients/Node/Core" "Core Client"
fix_lint "ConduitLLM.WebUI" "WebUI"

echo -e "\n📊 Summary:"
if [ $FIXED -eq 1 ]; then
    echo -e "${GREEN}✅ Some lint errors were auto-fixed!${NC}"
    echo -e "${YELLOW}⚠️  Please review the changes and test before committing${NC}"
else
    echo -e "${YELLOW}⚠️  No auto-fixable errors found${NC}"
fi

# Run validation to show current status
echo -e "\n🔍 Running validation to show current status..."
./scripts/validate-eslint.sh