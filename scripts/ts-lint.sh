#!/bin/bash

# Quick lint checker for WebUI and SDKs
# Usage: ./scripts/quick-lint-check.sh

set -e

# Color codes
readonly GREEN='\033[0;32m'
readonly RED='\033[0;31m'
readonly YELLOW='\033[1;33m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Helper functions
log_info() {
    echo -e "${GREEN}✅${NC} $1"
}

log_error() {
    echo -e "${RED}❌${NC} $1"
}

log_task() {
    echo -e "${CYAN}🔧${NC} $1"
}

# Print section header
print_section() {
    local title="$1"
    echo ""
    echo "═══ $title ═══"
}

# Check project root
if [[ ! -f "Conduit.sln" ]]; then
    log_error "Run this script from the Conduit root directory"
    exit 1
fi

print_section "QUICK LINT CHECK"

# Track results
WEBUI_ERRORS=0
ADMIN_SDK_ERRORS=0
CORE_SDK_ERRORS=0
TOTAL_ERRORS=0

# WebUI lint check
log_task "Checking WebUI lint..."
if [[ -d "ConduitLLM.WebUI" ]]; then
    cd ConduitLLM.WebUI
    LINT_OUTPUT=$(npm run lint 2>&1)
    
    # Extract actual ESLint errors - filter out npm noise
    ACTUAL_ERRORS=$(echo "$LINT_OUTPUT" | grep -E "^\./" || echo "$LINT_OUTPUT" | grep -E "^[[:space:]]*[0-9]+:[0-9]+[[:space:]]+[eE]rror" || true)
    ERROR_COUNT=$(echo "$LINT_OUTPUT" | grep -E "^[[:space:]]*[0-9]+:[0-9]+[[:space:]]+[eE]rror" | wc -l)
    
    if [[ $ERROR_COUNT -eq 0 ]]; then
        log_info "WebUI: No lint errors"
        WEBUI_ERRORS=0
    else
        log_error "WebUI: $ERROR_COUNT lint errors"
        echo ""
        echo "WebUI Code Defects:"
        echo "$ACTUAL_ERRORS"
        echo ""
        WEBUI_ERRORS=$ERROR_COUNT
    fi
    cd - > /dev/null
else
    log_error "WebUI directory not found"
    WEBUI_ERRORS=0
fi

# Admin SDK lint check
log_task "Checking Admin SDK lint..."
if [[ -d "SDKs/Node/Admin" ]]; then
    cd SDKs/Node/Admin
    LINT_OUTPUT=$(npm run lint 2>&1)
    
    # Extract actual ESLint errors - from filepath line through error line
    ACTUAL_ERRORS=$(echo "$LINT_OUTPUT" | sed -n '/^\/.*\.ts$/,/^[[:space:]]*[0-9]\+:[0-9]\+[[:space:]]\+error/p' || true)
    ERROR_COUNT=$(echo "$LINT_OUTPUT" | grep -E "^[[:space:]]*[0-9]+:[0-9]+[[:space:]]+error" | wc -l)
    
    if [[ $ERROR_COUNT -eq 0 ]]; then
        log_info "Admin SDK: No lint errors"
        ADMIN_SDK_ERRORS=0
    else
        log_error "Admin SDK: $ERROR_COUNT lint errors"
        echo ""
        echo "Admin SDK Code Defects:"
        echo "$ACTUAL_ERRORS"
        echo ""
        ADMIN_SDK_ERRORS=$ERROR_COUNT
    fi
    cd - > /dev/null
else
    log_error "Admin SDK directory not found"
    ADMIN_SDK_ERRORS=0
fi

# Core SDK lint check
log_task "Checking Core SDK lint..."
if [[ -d "SDKs/Node/Core" ]]; then
    cd SDKs/Node/Core
    LINT_OUTPUT=$(npm run lint 2>&1)
    
    # Extract actual ESLint errors - filter out npm noise
    ACTUAL_ERRORS=$(echo "$LINT_OUTPUT" | grep -B1 -A1 -E "^[[:space:]]*[0-9]+:[0-9]+[[:space:]]+error" || true)
    ERROR_COUNT=$(echo "$LINT_OUTPUT" | grep -E "^[[:space:]]*[0-9]+:[0-9]+[[:space:]]+error" | wc -l)
    
    if [[ $ERROR_COUNT -eq 0 ]]; then
        log_info "Core SDK: No lint errors"
        CORE_SDK_ERRORS=0
    else
        log_error "Core SDK: $ERROR_COUNT lint errors"
        echo ""
        echo "Core SDK Code Defects:"
        echo "$ACTUAL_ERRORS"
        echo ""
        CORE_SDK_ERRORS=$ERROR_COUNT
    fi
    cd - > /dev/null
else
    log_error "Core SDK directory not found"
    CORE_SDK_ERRORS=0
fi

# Summary
print_section "SUMMARY"
TOTAL_ERRORS=$((WEBUI_ERRORS + ADMIN_SDK_ERRORS + CORE_SDK_ERRORS))

echo "📊 WebUI errors: $WEBUI_ERRORS"
echo "📊 Admin SDK errors: $ADMIN_SDK_ERRORS"
echo "📊 Core SDK errors: $CORE_SDK_ERRORS"
echo "📊 Total errors: $TOTAL_ERRORS"

if [ $TOTAL_ERRORS -eq 0 ]; then
    log_info "🎉 All lint checks passed!"
    exit 0
else
    log_error "Found $TOTAL_ERRORS total lint errors"
    echo ""
    echo "To fix:"
    [ $WEBUI_ERRORS -gt 0 ] && echo "  WebUI: ./scripts/fix-webui-errors.sh --lint-only"
    [ $ADMIN_SDK_ERRORS -gt 0 ] && echo "  Admin SDK: ./scripts/fix-sdk-errors.sh admin"
    [ $CORE_SDK_ERRORS -gt 0 ] && echo "  Core SDK: ./scripts/fix-sdk-errors.sh core"
    exit 1
fi