#!/bin/bash

# Unified ESLint validation script
# Combines functionality from validate-eslint.sh and validate-eslint-strict.sh
# Usage: 
#   ./scripts/validate-eslint-unified.sh           # Normal mode (warnings allowed)
#   ./scripts/validate-eslint-unified.sh --strict  # Strict mode (errors only, CI/CD)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Track if any validation fails
FAILED=0
TOTAL_ERRORS=0
TOTAL_WARNINGS=0

# Parse command line arguments
STRICT_MODE=false
case "${1:-}" in
    "--strict"|"-s")
        STRICT_MODE=true
        echo -e "${BLUE}🔍 Running STRICT ESLint validation (CI/CD mode)...${NC}"
        ;;
    "--help"|"-h"|"help")
        cat << EOF
Usage: $0 [--strict]

This script validates ESLint configurations across all TypeScript projects.

Options:
  --strict, -s   Strict mode - fails on ANY errors (CI/CD mode)
  --help, -h     Show this help message

Normal mode:
- Reports errors and warnings
- Exits with error code only if linting fails to run
- Warnings are non-blocking

Strict mode:
- Reports errors and warnings
- Exits with error code if ANY errors are found
- Warnings are reported but non-blocking
- This is the same check that runs in CI/CD

Projects validated:
- SDKs/Node/Admin (Admin Client)
- SDKs/Node/Core (Core Client)
- ConduitLLM.WebUI (WebUI)
EOF
        exit 0
        ;;
    "")
        echo -e "${BLUE}🔍 Running ESLint validation (normal mode)...${NC}"
        ;;
    *)
        echo -e "${RED}❌ Error: Unknown argument '$1'${NC}" >&2
        echo "Use '$0 --help' for usage information" >&2
        exit 1
        ;;
esac

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
    LINT_EXIT_CODE=$?
    
    # Parse the output for errors and warnings
    local error_count=0
    local warning_count=0
    
    # Extract error count first
    error_count=$(echo "$LINT_OUTPUT" | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
    error_count=${error_count:-0}
    TOTAL_ERRORS=$((TOTAL_ERRORS + error_count))
    
    if [ $error_count -gt 0 ]; then
        echo -e "${RED}❌ Found $error_count error(s)${NC}"
        
        if [ "$STRICT_MODE" = true ]; then
            FAILED=1
        fi
        
        # Show the errors (limited to avoid spam)
        echo "$LINT_OUTPUT" | grep "error" | head -10
        if [ $(echo "$LINT_OUTPUT" | grep "error" | wc -l) -gt 10 ]; then
            echo "   ... and $(($(echo "$LINT_OUTPUT" | grep "error" | wc -l) - 10)) more errors"
        fi
    else
        echo -e "${GREEN}✅ No errors found${NC}"
    fi
    
    if echo "$LINT_OUTPUT" | grep -q "⚠.*warning"; then
        warning_count=$(echo "$LINT_OUTPUT" | grep -oE "[0-9]+ warning" | grep -oE "[0-9]+" | head -1)
        warning_count=${warning_count:-0}
        TOTAL_WARNINGS=$((TOTAL_WARNINGS + warning_count))
        echo -e "${YELLOW}⚠️  Found $warning_count warning(s) (non-blocking)${NC}"
    fi
    
    # In normal mode, only fail if ESLint couldn't run at all
    if [ "$STRICT_MODE" = false ] && [ $LINT_EXIT_CODE -ne 0 ] && [ $error_count -eq 0 ]; then
        echo -e "${RED}❌ ESLint execution failed${NC}"
        FAILED=1
    fi
    
    cd - >/dev/null
}

# Validate all TypeScript projects
validate_eslint "SDKs/Node/Admin" "Admin Client"
validate_eslint "SDKs/Node/Core" "Core Client"
validate_eslint "ConduitLLM.WebUI" "WebUI"

# Print summary
echo -e "\n📊 Summary:"
echo -e "Total Errors: ${TOTAL_ERRORS}"
echo -e "Total Warnings: ${TOTAL_WARNINGS}"

if [ $FAILED -eq 0 ]; then
    if [ "$STRICT_MODE" = true ]; then
        echo -e "${GREEN}✅ All ESLint validations passed! (No errors)${NC}"
        if [ $TOTAL_WARNINGS -gt 0 ]; then
            echo -e "${YELLOW}⚠️  Consider fixing the $TOTAL_WARNINGS warning(s) for better code quality${NC}"
        fi
    else
        echo -e "${GREEN}✅ All ESLint configurations are valid!${NC}"
        if [ $TOTAL_ERRORS -gt 0 ]; then
            echo -e "${YELLOW}⚠️  Found $TOTAL_ERRORS error(s) - consider running with --strict to enforce fixes${NC}"
        fi
        if [ $TOTAL_WARNINGS -gt 0 ]; then
            echo -e "${YELLOW}⚠️  Found $TOTAL_WARNINGS warning(s) for better code quality${NC}"
        fi
    fi
    exit 0
else
    if [ "$STRICT_MODE" = true ]; then
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
    else
        echo -e "${RED}❌ ESLint validation failed!${NC}"
        echo -e "${YELLOW}⚠️  Fix the issues above before pushing to avoid CI/CD failures${NC}"
    fi
    exit 1
fi