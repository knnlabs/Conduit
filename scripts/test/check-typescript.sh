#!/bin/bash

# Comprehensive TypeScript Error Checking Script
# Checks ALL TypeScript projects for lint and build errors
# Usage: 
#   ./scripts/check-typescript.sh           # Check all projects
#   ./scripts/check-typescript.sh --json    # Output in JSON format
#   ./scripts/check-typescript.sh --fix     # Attempt auto-fixes first

set -e

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly CYAN='\033[0;36m'
readonly MAGENTA='\033[0;35m'
readonly NC='\033[0m' # No Color

# Configuration
LOG_FILE="typescript-errors-$(date +%Y%m%d-%H%M%S).log"
JSON_OUTPUT=false
ATTEMPT_FIX=false
VERBOSE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --json)
            JSON_OUTPUT=true
            shift
            ;;
        --fix)
            ATTEMPT_FIX=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --help|-h)
            cat << EOF
Comprehensive TypeScript Error Checking Script

Usage: $0 [options]

Options:
  --json      Output results in JSON format for easy parsing
  --fix       Attempt to auto-fix errors before reporting
  --verbose   Show detailed output during checks
  --help      Show this help message

This script checks all TypeScript projects:
- WebAdmin (Next.js application)
- Admin SDK (Node.js)
- Core SDK (Node.js)
- Common SDK (Node.js)
- Script utilities

For each project, it runs:
1. ESLint checks
2. TypeScript compilation checks
3. Build process (where applicable)

The output provides a comprehensive report of all errors.
EOF
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Global error tracking
declare -A PROJECT_ERRORS
declare -A PROJECT_WARNINGS
declare -A PROJECT_BUILD_STATUS
TOTAL_ERRORS=0
TOTAL_WARNINGS=0
FAILED_PROJECTS=()

# Helper functions
log_info() {
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "${GREEN}✅${NC} $1"
    echo "[INFO] $1" >> "$LOG_FILE"
}

log_warn() {
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "${YELLOW}⚠️${NC} $1"
    echo "[WARN] $1" >> "$LOG_FILE"
}

log_error() {
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "${RED}❌${NC} $1"
    echo "[ERROR] $1" >> "$LOG_FILE"
}

log_task() {
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "${CYAN}🔧${NC} $1"
    echo "[TASK] $1" >> "$LOG_FILE"
}

log_section() {
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "\n${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "${MAGENTA}  $1${NC}"
    [[ "$JSON_OUTPUT" != "true" ]] && echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo "\n========== $1 ==========" >> "$LOG_FILE"
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Extract error counts from output
count_errors() {
    local output="$1"
    local error_count=0
    local warning_count=0
    
    # Try different patterns for counting errors
    if echo "$output" | grep -q "[0-9]\+ error"; then
        error_count=$(echo "$output" | grep -oE "[0-9]+ error" | grep -oE "[0-9]+" | head -1)
    elif echo "$output" | grep -q "✖ [0-9]\+ problem"; then
        error_count=$(echo "$output" | grep -oE "✖ [0-9]+ problem" | grep -oE "[0-9]+" | head -1)
    fi
    
    # Count warnings
    if echo "$output" | grep -q "[0-9]\+ warning"; then
        warning_count=$(echo "$output" | grep -oE "[0-9]+ warning" | grep -oE "[0-9]+" | head -1)
    fi
    
    echo "${error_count:-0} ${warning_count:-0}"
}

# Check WebAdmin
check_webadmin() {
    local project_name="WebAdmin"
    log_section "Checking WebAdmin (Next.js Application)"
    
    if [[ ! -d "WebAdmin" ]]; then
        log_error "WebAdmin directory not found"
        PROJECT_ERRORS["$project_name"]="Directory not found"
        FAILED_PROJECTS+=("$project_name")
        return 1
    fi
    
    cd WebAdmin
    
    local lint_errors=0
    local lint_warnings=0
    local type_errors=0
    local build_errors=0
    
    # Check for package.json
    if [[ ! -f "package.json" ]]; then
        log_error "package.json not found in WebAdmin"
        PROJECT_ERRORS["$project_name"]="package.json missing"
        cd ..
        return 1
    fi
    
    # Install dependencies if needed
    if [[ ! -d "node_modules" ]]; then
        log_task "Installing WebAdmin dependencies..."
        npm install > /dev/null 2>&1 || true
    fi
    
    # Run ESLint
    log_task "Running ESLint on WebAdmin..."
    
    if [[ "$ATTEMPT_FIX" == "true" ]]; then
        log_task "Attempting ESLint auto-fix..."
        npm run lint:fix > /dev/null 2>&1 || true
    fi
    
    local lint_output
    lint_output=$(npm run lint 2>&1 || true)
    echo "$lint_output" >> "$LOG_FILE"
    
    read -r lint_errors lint_warnings <<< $(count_errors "$lint_output")
    
    if [[ $lint_errors -gt 0 ]]; then
        log_error "WebAdmin ESLint: $lint_errors errors, $lint_warnings warnings"
        # Capture specific errors for report
        echo "\n--- WebAdmin ESLint Errors ---" >> "$LOG_FILE"
        echo "$lint_output" | grep -E "error|Error" | head -50 >> "$LOG_FILE"
    else
        log_info "WebAdmin ESLint: No errors found"
    fi
    
    # Run TypeScript type checking
    log_task "Running TypeScript type check on WebAdmin..."
    
    local type_output
    type_output=$(npm run type-check 2>&1 || true)
    echo "$type_output" >> "$LOG_FILE"
    
    if echo "$type_output" | grep -q "error TS"; then
        type_errors=$(echo "$type_output" | grep -c "error TS" || echo "0")
        log_error "WebAdmin TypeScript: $type_errors type errors"
        echo "\n--- WebAdmin TypeScript Errors ---" >> "$LOG_FILE"
        echo "$type_output" | grep "error TS" | head -50 >> "$LOG_FILE"
    else
        log_info "WebAdmin TypeScript: No type errors found"
    fi
    
    # Note: We do NOT run build for WebAdmin in development
    log_warn "WebAdmin build check skipped (breaks development container)"
    
    # Store results
    PROJECT_ERRORS["$project_name"]=$((lint_errors + type_errors))
    PROJECT_WARNINGS["$project_name"]=$lint_warnings
    PROJECT_BUILD_STATUS["$project_name"]="Skipped (Dev Safety)"
    
    if [[ $((lint_errors + type_errors)) -gt 0 ]]; then
        FAILED_PROJECTS+=("$project_name")
    fi
    
    TOTAL_ERRORS=$((TOTAL_ERRORS + lint_errors + type_errors))
    TOTAL_WARNINGS=$((TOTAL_WARNINGS + lint_warnings))
    
    cd ..
}

# Check SDK projects
check_sdk() {
    local sdk_path="$1"
    local sdk_name="$2"
    
    log_section "Checking $sdk_name SDK"
    
    if [[ ! -d "$sdk_path" ]]; then
        log_error "$sdk_name directory not found at $sdk_path"
        PROJECT_ERRORS["$sdk_name"]="Directory not found"
        FAILED_PROJECTS+=("$sdk_name")
        return 1
    fi
    
    cd "$sdk_path"
    
    local lint_errors=0
    local lint_warnings=0
    local build_errors=0
    
    # Check for package.json
    if [[ ! -f "package.json" ]]; then
        log_error "package.json not found in $sdk_name"
        PROJECT_ERRORS["$sdk_name"]="package.json missing"
        cd - > /dev/null
        return 1
    fi
    
    # Install dependencies if needed
    if [[ ! -d "node_modules" ]]; then
        log_task "Installing $sdk_name dependencies..."
        npm install > /dev/null 2>&1 || true
    fi
    
    # Run ESLint if available
    if npm run 2>/dev/null | grep -q "^  lint$"; then
        log_task "Running ESLint on $sdk_name..."
        
        if [[ "$ATTEMPT_FIX" == "true" ]] && npm run 2>/dev/null | grep -q "lint:fix"; then
            log_task "Attempting ESLint auto-fix..."
            npm run lint:fix > /dev/null 2>&1 || true
        elif [[ "$ATTEMPT_FIX" == "true" ]]; then
            npm run lint -- --fix > /dev/null 2>&1 || true
        fi
        
        local lint_output
        lint_output=$(npm run lint 2>&1 || true)
        echo "$lint_output" >> "$LOG_FILE"
        
        read -r lint_errors lint_warnings <<< $(count_errors "$lint_output")
        
        if [[ $lint_errors -gt 0 ]]; then
            log_error "$sdk_name ESLint: $lint_errors errors, $lint_warnings warnings"
            echo "\n--- $sdk_name ESLint Errors ---" >> "$LOG_FILE"
            echo "$lint_output" | grep -E "error|Error" | head -50 >> "$LOG_FILE"
        else
            log_info "$sdk_name ESLint: No errors found"
        fi
    else
        log_warn "$sdk_name: No lint script found"
    fi
    
    # Run TypeScript build
    log_task "Building $sdk_name..."
    
    local build_output
    build_output=$(npm run build 2>&1 || true)
    echo "$build_output" >> "$LOG_FILE"
    
    if echo "$build_output" | grep -q "error TS\|Error:\|ERROR\|Failed"; then
        build_errors=$(echo "$build_output" | grep -cE "error TS|Error:|ERROR" || echo "1")
        log_error "$sdk_name Build: $build_errors errors"
        echo "\n--- $sdk_name Build Errors ---" >> "$LOG_FILE"
        echo "$build_output" | grep -E "error TS|Error:|ERROR" | head -50 >> "$LOG_FILE"
        PROJECT_BUILD_STATUS["$sdk_name"]="Failed"
    else
        log_info "$sdk_name Build: Success"
        PROJECT_BUILD_STATUS["$sdk_name"]="Success"
    fi
    
    # Store results
    PROJECT_ERRORS["$sdk_name"]=$((lint_errors + build_errors))
    PROJECT_WARNINGS["$sdk_name"]=$lint_warnings
    
    if [[ $((lint_errors + build_errors)) -gt 0 ]]; then
        FAILED_PROJECTS+=("$sdk_name")
    fi
    
    TOTAL_ERRORS=$((TOTAL_ERRORS + lint_errors + build_errors))
    TOTAL_WARNINGS=$((TOTAL_WARNINGS + lint_warnings))
    
    cd - > /dev/null
}

# Generate summary report
generate_report() {
    if [[ "$JSON_OUTPUT" == "true" ]]; then
        # Generate JSON output
        cat << EOF
{
  "timestamp": "$(date -Iseconds)",
  "totalErrors": $TOTAL_ERRORS,
  "totalWarnings": $TOTAL_WARNINGS,
  "failedProjects": [$(printf '"%s",' "${FAILED_PROJECTS[@]}" | sed 's/,$//')],
  "projects": {
EOF
        
        local first=true
        for project in "${!PROJECT_ERRORS[@]}"; do
            [[ "$first" != "true" ]] && echo ","
            printf '    "%s": {\n' "$project"
            printf '      "errors": %d,\n' "${PROJECT_ERRORS[$project]}"
            printf '      "warnings": %d,\n' "${PROJECT_WARNINGS[$project]:-0}"
            printf '      "buildStatus": "%s"\n' "${PROJECT_BUILD_STATUS[$project]:-Unknown}"
            printf '    }'
            first=false
        done
        
        cat << EOF

  },
  "logFile": "$LOG_FILE"
}
EOF
    else
        # Generate human-readable report
        echo ""
        echo -e "${MAGENTA}═══════════════════════════════════════════════════════${NC}"
        echo -e "${MAGENTA}           TYPESCRIPT ERROR CHECK SUMMARY              ${NC}"
        echo -e "${MAGENTA}═══════════════════════════════════════════════════════${NC}"
        echo ""
        
        # Project summary table
        printf "%-20s │ %-10s │ %-10s │ %-15s\n" "Project" "Errors" "Warnings" "Build Status"
        echo "─────────────────────┼────────────┼────────────┼─────────────────"
        
        for project in "WebAdmin" "Admin SDK" "Core SDK" "Common SDK"; do
            if [[ -n "${PROJECT_ERRORS[$project]}" ]]; then
                local error_color="$GREEN"
                [[ ${PROJECT_ERRORS[$project]} -gt 0 ]] && error_color="$RED"
                
                local warn_color="$GREEN"
                [[ ${PROJECT_WARNINGS[$project]:-0} -gt 0 ]] && warn_color="$YELLOW"
                
                local build_color="$GREEN"
                [[ "${PROJECT_BUILD_STATUS[$project]}" == "Failed" ]] && build_color="$RED"
                [[ "${PROJECT_BUILD_STATUS[$project]}" == "Skipped"* ]] && build_color="$YELLOW"
                
                printf "%-20s │ " "$project"
                printf "${error_color}%-10s${NC} │ " "${PROJECT_ERRORS[$project]}"
                printf "${warn_color}%-10s${NC} │ " "${PROJECT_WARNINGS[$project]:-0}"
                printf "${build_color}%-15s${NC}\n" "${PROJECT_BUILD_STATUS[$project]:-N/A}"
            fi
        done
        
        echo ""
        echo "─────────────────────────────────────────────────────────"
        echo -e "${CYAN}Total Errors:${NC} ${RED}$TOTAL_ERRORS${NC}"
        echo -e "${CYAN}Total Warnings:${NC} ${YELLOW}$TOTAL_WARNINGS${NC}"
        echo ""
        
        if [[ ${#FAILED_PROJECTS[@]} -gt 0 ]]; then
            echo -e "${RED}Failed Projects:${NC} ${FAILED_PROJECTS[*]}"
        else
            echo -e "${GREEN}All projects passed!${NC}"
        fi
        
        echo ""
        echo -e "${CYAN}Detailed log saved to:${NC} $LOG_FILE"
        echo ""
        
        # Quick fix suggestions
        if [[ $TOTAL_ERRORS -gt 0 ]]; then
            echo -e "${YELLOW}═══════════════════════════════════════════════════════${NC}"
            echo -e "${YELLOW}                  QUICK FIX COMMANDS                   ${NC}"
            echo -e "${YELLOW}═══════════════════════════════════════════════════════${NC}"
            echo ""
            
            if [[ ${PROJECT_ERRORS["WebAdmin"]:-0} -gt 0 ]]; then
                echo "WebAdmin fixes:"
                echo "  ./scripts/fix-webadmin-errors.sh --lint-only"
                echo ""
            fi
            
            if [[ ${PROJECT_ERRORS["Admin SDK"]:-0} -gt 0 ]] || [[ ${PROJECT_ERRORS["Core SDK"]:-0} -gt 0 ]]; then
                echo "SDK fixes:"
                echo "  ./scripts/fix-sdk-errors.sh"
                echo ""
            fi
            
            echo "To attempt auto-fixes for all projects:"
            echo "  $0 --fix"
            echo ""
        fi
        
        # Extract and show sample errors
        if [[ $TOTAL_ERRORS -gt 0 ]] && [[ "$VERBOSE" != "true" ]]; then
            echo -e "${YELLOW}═══════════════════════════════════════════════════════${NC}"
            echo -e "${YELLOW}                    SAMPLE ERRORS                      ${NC}"
            echo -e "${YELLOW}═══════════════════════════════════════════════════════${NC}"
            echo ""
            echo "First 10 errors from log (use --verbose for full output):"
            echo ""
            grep -E "\[ERROR\]|error TS|Error:|ERROR" "$LOG_FILE" | head -10
            echo ""
            echo "For full error details, see: $LOG_FILE"
        fi
    fi
}

# Main execution
main() {
    # Initialize log file
    echo "TypeScript Error Check - $(date)" > "$LOG_FILE"
    echo "========================================" >> "$LOG_FILE"
    
    if [[ "$JSON_OUTPUT" != "true" ]]; then
        echo -e "${CYAN}🔍 TypeScript Error Checker${NC}"
        echo -e "${CYAN}Checking all TypeScript projects for errors...${NC}"
        echo ""
    fi
    
    # Check WebAdmin
    check_webadmin
    
    # Check SDKs
    check_sdk "SDKs/Node/Admin" "Admin SDK"
    check_sdk "SDKs/Node/Core" "Core SDK"
    check_sdk "SDKs/Node/Common" "Common SDK"
    
    # Generate report
    generate_report
    
    # Exit with appropriate code
    if [[ $TOTAL_ERRORS -gt 0 ]]; then
        exit 1
    else
        exit 0
    fi
}

# Run main function
main