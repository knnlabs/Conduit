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
if cd ConduitLLM.WebUI && npm run lint --silent 2>/dev/null; then
    log_info "WebUI: No lint errors"
    cd - > /dev/null
else
    WEBUI_ERRORS=$(cd ConduitLLM.WebUI && npm run lint 2>&1 | grep -c "error" || echo "0")
    log_error "WebUI: $WEBUI_ERRORS lint errors"
    cd - > /dev/null
fi

# Admin SDK lint check
log_task "Checking Admin SDK lint..."
if cd SDKs/Node/Admin && npm run lint --silent 2>/dev/null; then
    log_info "Admin SDK: No lint errors"
    cd - > /dev/null
else
    ADMIN_SDK_ERRORS=$(cd SDKs/Node/Admin && npm run lint 2>&1 | grep -c "error" || echo "0")
    log_error "Admin SDK: $ADMIN_SDK_ERRORS lint errors"
    cd - > /dev/null
fi

# Core SDK lint check
log_task "Checking Core SDK lint..."
if cd SDKs/Node/Core && npm run lint --silent 2>/dev/null; then
    log_info "Core SDK: No lint errors"
    cd - > /dev/null
else
    CORE_SDK_ERRORS=$(cd SDKs/Node/Core && npm run lint 2>&1 | grep -c "error" || echo "0")
    log_error "Core SDK: $CORE_SDK_ERRORS lint errors"
    cd - > /dev/null
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