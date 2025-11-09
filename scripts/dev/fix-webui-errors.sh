#!/bin/bash

# Safe WebUI lint and build script with permission detection and environment validation
# Usage: 
#   ./scripts/fix-webui-errors.sh           # Full workflow
#   ./scripts/fix-webui-errors.sh --lint-only    # Lint validation only
#   ./scripts/fix-webui-errors.sh --build-only   # Skip lint, build only  
#   ./scripts/fix-webui-errors.sh --check-only   # Check environment/permissions only

set -e

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Global state
PERMISSION_ISSUES=false
LINT_ERRORS=0
BUILD_FAILED=false
ENVIRONMENT_ISSUES=false

# Helper functions
log_info() {
    echo -e "${GREEN}✅${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}⚠️${NC} $1"
}

log_error() {
    echo -e "${RED}❌${NC} $1"
}

log_task() {
    echo -e "${CYAN}🔧${NC} $1"
}

log_stats() {
    echo -e "${CYAN}📊${NC} $1"
}

# Print section header
print_section_header() {
    local title="$1"
    local border_char="═"
    local corner_tl="╔"
    local corner_tr="╗"
    local corner_bl="╚"
    local corner_br="╝"
    local vertical="║"
    
    local header_length=${#title}
    local total_width=$((header_length + 6))
    local border=$(printf "%*s" $total_width | tr ' ' "$border_char")
    local padding=$(printf "%*s" 3)
    
    echo ""
    echo "${corner_tl}${border}${corner_tr}"
    echo "${vertical}${padding}${title}${padding}${vertical}"
    echo "${corner_bl}${border}${corner_br}"
}

show_usage() {
    cat << EOF
WebUI Lint and Build Script

Usage: $0 [options]

Options:
  --lint-only     Run linting and fixing only (skip build)
  --build-only    Run build only (skip linting)
  --check-only    Check environment and permissions only
  --help          Show this help message

This script safely validates the WebUI development environment and runs 
linting and build processes with proper error detection and guidance.

The script will:
1. Validate development environment setup
2. Check file and folder permissions (detection only)
3. Run ESLint auto-fix and validation
4. Run TypeScript type checking
5. Run the build process

If permission issues are detected, the script will provide guidance
on using ./scripts/start-dev.sh --clean to fix them properly.

EOF
}

# Check if we're in the correct directory
check_project_root() {
    log_task "Validating project structure..."
    
    if [[ ! -f "Conduit.sln" ]]; then
        log_error "This script must be run from the Conduit root directory"
        log_error "Current directory: $(pwd)"
        exit 1
    fi
    
    if [[ ! -d "ConduitLLM.WebUI" ]]; then
        log_error "ConduitLLM.WebUI directory not found"
        exit 1
    fi
    
    if [[ ! -f "ConduitLLM.WebUI/package.json" ]]; then
        log_error "ConduitLLM.WebUI/package.json not found"
        exit 1
    fi
    
    log_info "Project structure validated"
}

# Validate development environment context
check_development_environment() {
    log_task "Checking development environment..."
    
    # Check if Docker is running
    if ! docker info >/dev/null 2>&1; then
        log_warn "Docker is not running - host-based development assumed"
        return 0
    fi
    
    # Check if development containers are running
    local webui_containers=$(docker ps --filter "name=conduit-webui" --format "{{.Names}}\t{{.Image}}" 2>/dev/null || echo "")
    
    if [[ -n "$webui_containers" ]]; then
        # Check if using development image
        if echo "$webui_containers" | grep -q "node:22-alpine"; then
            log_info "Development containers detected and running"
        else
            log_error "Production containers detected (not development setup)"
            log_error "Found: $webui_containers"
            log_error "To fix: docker compose down --volumes --remove-orphans"
            log_error "Then run: ./scripts/start-dev.sh"
            ENVIRONMENT_ISSUES=true
            return 1
        fi
    else
        log_warn "No WebUI containers running - host-based development assumed"
        log_warn "Ensure you have Node.js and npm installed for host development"
    fi
    
    return 0
}

# Test write access to a directory
test_write_access() {
    local target_dir="$1"
    local test_file="$target_dir/.write-test-$$"
    
    if [[ ! -d "$target_dir" ]]; then
        return 1
    fi
    
    if ! touch "$test_file" 2>/dev/null; then
        return 1
    fi
    
    rm -f "$test_file" 2>/dev/null
    return 0
}

# Check file and folder permissions
check_permissions() {
    log_task "Checking file and folder permissions..."
    
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    local issues_found=false
    
    # Check WebUI source directory permissions
    if ! test_write_access "./ConduitLLM.WebUI"; then
        log_error "Cannot write to ConduitLLM.WebUI directory"
        log_error "Fix with: sudo chown -R $USER:$USER ./ConduitLLM.WebUI"
        issues_found=true
    fi
    
    # Check .next directory if it exists
    if [[ -d "./ConduitLLM.WebUI/.next" ]]; then
        if ! test_write_access "./ConduitLLM.WebUI/.next"; then
            log_error "Cannot write to .next folder"
            log_error "This will cause build failures"
            log_error "Fix with: ./scripts/start-dev.sh --fix-perms"
            log_error "Or for full cleanup: ./scripts/start-dev.sh --clean"
            issues_found=true
        else
            # Check ownership
            local next_owner=$(stat -c "%u:%g" "./ConduitLLM.WebUI/.next" 2>/dev/null || echo "unknown")
            if [[ "$next_owner" != "$current_uid:$current_gid" ]] && [[ "$next_owner" != "unknown" ]]; then
                log_warn ".next folder ownership mismatch: $next_owner (expected: $current_uid:$current_gid)"
                log_warn "This may cause permission issues during build"
                log_warn "Fix with: ./scripts/start-dev.sh --fix-perms"
                log_warn "Or for full cleanup: ./scripts/start-dev.sh --clean"
                issues_found=true
            fi
        fi
    fi
    
    # Check node_modules directory if it exists
    if [[ -d "./ConduitLLM.WebUI/node_modules" ]]; then
        if ! test_write_access "./ConduitLLM.WebUI/node_modules"; then
            log_error "Cannot write to node_modules folder"
            log_error "This will cause npm install failures"
            log_error "Fix with: ./scripts/start-dev.sh --fix-perms"
            log_error "Or for full cleanup: ./scripts/start-dev.sh --clean"
            issues_found=true
        fi
    fi
    
    # Check for specific build artifact directories
    local build_dirs=("./ConduitLLM.WebUI/.next/cache" "./ConduitLLM.WebUI/.next/static")
    for dir in "${build_dirs[@]}"; do
        if [[ -d "$dir" ]] && ! test_write_access "$dir"; then
            log_error "Cannot write to build directory: $dir"
            log_error "Fix with: ./scripts/start-dev.sh --fix-perms"
            log_error "Or for full cleanup: ./scripts/start-dev.sh --clean"
            issues_found=true
        fi
    done
    
    if [[ "$issues_found" == "true" ]]; then
        PERMISSION_ISSUES=true
        log_error "Permission issues detected - builds may fail"
        echo ""
        log_error "RECOMMENDED FIXES:"
        log_error "1. Quick permission fix: ./scripts/start-dev.sh --fix-perms"
        log_error "2. Full environment cleanup: ./scripts/start-dev.sh --clean"
        log_error "3. Manual fix (if above fail): sudo chown -R \$USER:\$USER ./ConduitLLM.WebUI"
        echo ""
        return 1
    else
        log_info "All permission checks passed"
        return 0
    fi
}

# Check if npm script exists
has_npm_script() {
    local script_name="$1"
    (cd ConduitLLM.WebUI && npm run 2>/dev/null | grep -q "^  $script_name$")
}

# Run ESLint auto-fix and validation
run_eslint() {
    log_task "Running ESLint validation and auto-fix..."
    
    local original_dir=$(pwd)
    
    # Step 1: Auto-fix what can be fixed
    log_task "Step 1: Running ESLint auto-fix..."
    if has_npm_script "lint:fix"; then
        if (cd ConduitLLM.WebUI && npm run lint:fix 2>/dev/null); then
            log_info "ESLint auto-fix completed"
        else
            log_warn "ESLint auto-fix encountered issues (this is normal)"
        fi
    else
        log_warn "No lint:fix script found, trying direct ESLint fix"
        if (cd ConduitLLM.WebUI && npx next lint --fix 2>/dev/null); then
            log_info "ESLint auto-fix completed"
        else
            log_warn "ESLint auto-fix encountered issues (this is normal)"
        fi
    fi
    
    # Step 2: Validate linting
    log_task "Step 2: Running ESLint validation..."
    local lint_output
    local lint_exit_code=0
    
    if has_npm_script "lint"; then
        lint_output=$(cd ConduitLLM.WebUI && npm run lint 2>&1) || lint_exit_code=$?
    else
        lint_output=$(cd ConduitLLM.WebUI && npx next lint 2>&1) || lint_exit_code=$?
    fi
    
    # Count errors
    local error_count=$(echo "$lint_output" | grep -c "error" 2>/dev/null || echo "0")
    local warning_count=$(echo "$lint_output" | grep -c "warning" 2>/dev/null || echo "0")
    
    if [[ $lint_exit_code -eq 0 ]]; then
        log_info "ESLint validation passed"
        log_stats "Warnings: $warning_count"
    else
        log_error "ESLint validation failed"
        log_stats "Errors: $error_count, Warnings: $warning_count"
        
        # Show first 10 errors for guidance
        echo ""
        log_error "First 10 ESLint errors:"
        echo "$lint_output" | grep "error" | head -10 | while read -r line; do
            echo "  $line"
        done
        echo ""
        
        LINT_ERRORS=$error_count
    fi
    
    return $lint_exit_code
}

# Run TypeScript type checking
run_type_check() {
    log_task "Running TypeScript type checking..."
    
    local type_check_exit_code=0
    
    if has_npm_script "type-check"; then
        log_info "Using npm run type-check"
        if (cd ConduitLLM.WebUI && npm run type-check 2>/dev/null); then
            log_info "TypeScript type checking passed"
        else
            type_check_exit_code=$?
            log_error "TypeScript type checking failed"
        fi
    elif (cd ConduitLLM.WebUI && command -v tsc >/dev/null); then
        log_warn "No type-check script found, using tsc directly"
        if (cd ConduitLLM.WebUI && npx tsc --noEmit 2>/dev/null); then
            log_info "TypeScript type checking passed"
        else
            type_check_exit_code=$?
            log_error "TypeScript type checking failed"
        fi
    else
        log_warn "TypeScript checking skipped - tsc not available"
        log_warn "Install TypeScript: npm install -g typescript"
    fi
    
    return $type_check_exit_code
}

# Check if WebUI container is running and stop it if needed
stop_webui_container() {
    # Find any container running on port 3000 (WebUI port)
    local container_id=$(docker ps --format "{{.ID}}" --filter "publish=3000" | head -1)
    local was_running=false
    
    if [[ -n "$container_id" ]]; then
        was_running=true
        local container_name=$(docker inspect --format='{{.Name}}' "$container_id" | sed 's/^\/*//')
        log_warn "WebUI development container is running: $container_name"
        log_task "Stopping WebUI container to prevent build conflicts..."
        
        if docker stop "$container_id" >/dev/null 2>&1; then
            log_info "WebUI container stopped successfully"
            # Store container ID for restart
            echo "$container_id"
            return 0
        else
            log_error "Failed to stop WebUI container"
            return 1
        fi
    else
        log_info "No WebUI container running on port 3000 - safe to build"
    fi
    
    echo ""
    return 0
}

# Restart WebUI container if it was running before
restart_webui_container() {
    local container_id="$1"
    
    if [[ -z "$container_id" ]]; then
        return 0
    fi
    
    log_task "Restarting WebUI development container..."
    
    if docker start "$container_id" >/dev/null 2>&1; then
        log_info "WebUI container restarted"
        
        # Wait for container to be ready
        log_task "Waiting for WebUI to be ready..."
        local max_attempts=30
        local attempt=0
        
        while [[ $attempt -lt $max_attempts ]]; do
            if docker logs "$container_id" 2>&1 | tail -n 20 | grep -q "Ready in"; then
                log_info "WebUI is ready"
                return 0
            fi
            sleep 1
            ((attempt++))
        done
        
        log_warn "WebUI container started but may not be fully ready"
    else
        log_error "Failed to restart WebUI container"
        log_error "To restart manually: docker start $container_id"
        return 1
    fi
}

# Run build process
run_build() {
    log_task "Running build process..."
    
    if [[ "$PERMISSION_ISSUES" == "true" ]]; then
        log_warn "Permission issues were detected earlier"
        log_warn "Build may fail due to permission problems"
        echo ""
    fi
    
    # Check and stop WebUI container if running
    local container_id=$(stop_webui_container)
    local stop_status=$?
    
    if [[ $stop_status -ne 0 ]]; then
        log_error "Failed to stop WebUI container, aborting build"
        return 1
    fi
    
    local build_start_time=$(date +%s)
    local build_exit_code=0
    
    if has_npm_script "build"; then
        log_info "Using npm run build"
        if (cd ConduitLLM.WebUI && npm run build); then
            local build_end_time=$(date +%s)
            local build_duration=$((build_end_time - build_start_time))
            log_info "Build completed successfully in ${build_duration}s"
        else
            build_exit_code=$?
            log_error "Build failed"
            BUILD_FAILED=true
        fi
    else
        log_error "No build script found in package.json"
        BUILD_FAILED=true
    fi
    
    # Restart container if it was running before
    if [[ -n "$container_id" ]]; then
        restart_webui_container "$container_id" || log_error "Please restart the development environment manually"
    fi
    
    return $build_exit_code
}

# Print final summary
print_summary() {
    print_section_header "SUMMARY"
    
    if [[ "$ENVIRONMENT_ISSUES" == "true" ]]; then
        log_error "Environment validation failed"
        log_error "Fix development environment setup first"
        return 1
    fi
    
    if [[ "$PERMISSION_ISSUES" == "true" ]]; then
        log_error "Permission issues detected"
        log_error "Run: ./scripts/start-dev.sh --clean"
    else
        log_info "No permission issues found"
    fi
    
    if [[ $LINT_ERRORS -gt 0 ]]; then
        log_error "ESLint errors: $LINT_ERRORS"
        log_error "Fix manually or use: cd ConduitLLM.WebUI && npm run lint:fix"
    else
        log_info "ESLint validation passed"
    fi
    
    if [[ "$BUILD_FAILED" == "true" ]]; then
        log_error "Build failed"
        if [[ "$PERMISSION_ISSUES" == "true" ]]; then
            log_error "Likely cause: Permission issues"
            log_error "Fix: ./scripts/start-dev.sh --clean"
        fi
    else
        log_info "Build completed successfully"
    fi
    
    # Overall status
    if [[ "$ENVIRONMENT_ISSUES" == "false" ]] && [[ "$PERMISSION_ISSUES" == "false" ]] && [[ $LINT_ERRORS -eq 0 ]] && [[ "$BUILD_FAILED" == "false" ]]; then
        echo ""
        log_info "🎉 All checks passed - WebUI is ready!"
        return 0
    else
        echo ""
        log_error "❌ Issues found - see summary above for fixes"
        return 1
    fi
}

# Main function
main() {
    local lint_only=false
    local build_only=false
    local check_only=false
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --lint-only)
                lint_only=true
                shift
                ;;
            --build-only)
                build_only=true
                shift
                ;;
            --check-only)
                check_only=true
                shift
                ;;
            --help|-h)
                show_usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done
    
    print_section_header "WEBUI LINT AND BUILD VALIDATION"
    
    # Always run basic checks
    check_project_root
    check_development_environment || ENVIRONMENT_ISSUES=true
    check_permissions || true  # Don't exit on permission issues, just record them
    
    if [[ "$check_only" == "true" ]]; then
        print_summary
        exit $?
    fi
    
    # Exit early if environment issues
    if [[ "$ENVIRONMENT_ISSUES" == "true" ]]; then
        print_summary
        exit 1
    fi
    
    # Run linting unless build-only
    if [[ "$build_only" != "true" ]]; then
        run_eslint || true  # Don't exit on lint errors
        run_type_check || true  # Don't exit on type errors
    fi
    
    # Run build unless lint-only
    if [[ "$lint_only" != "true" ]]; then
        run_build || true  # Don't exit on build errors
    fi
    
    print_summary
    exit $?
}

# Run main function with all arguments
main "$@"