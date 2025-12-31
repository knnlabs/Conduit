#!/usr/bin/env bash
# =============================================================================
# Conduit Development Workflow Script
# =============================================================================
# Provides convenient development commands for working with the WebAdmin and SDKs
# without stopping Docker containers. Handles permissions correctly.
# =============================================================================

set -euo pipefail

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Configuration
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
readonly WEBADMIN_SERVICE="webadmin"

# Helper functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_task() {
    echo -e "${CYAN}[TASK]${NC} $1"
}

show_usage() {
    cat << EOF
Usage: $0 <command> [options]

Development Commands (Container):
  build-webadmin          - Build the WebAdmin application
  build-sdks           - Build all SDK packages (Common, Admin, Core)
  build-sdk <name>     - Build specific SDK (common|admin|core)
  lint-webadmin           - Run ESLint on WebAdmin
  lint-fix-webadmin       - Run ESLint with --fix on WebAdmin
  type-check-webadmin     - Run TypeScript type checking on WebAdmin
  test-webadmin           - Run WebAdmin tests
  npm-install-webadmin    - Install WebAdmin dependencies
  npm-install-sdks     - Install all SDK dependencies
  shell                - Open bash shell in WebAdmin container
  logs                 - Show WebAdmin container logs
  restart-webadmin        - Restart WebAdmin container
  status               - Show container status
  exec <cmd>           - Execute any command in WebAdmin container

Local Build Commands (No Container Required):
  install-local        - Install all dependencies locally (SDKs + WebAdmin)
  build-local          - Build all TypeScript projects locally
  install-and-build-local - Install and build everything locally (fresh clone)

Utility Commands:
  fix-permissions      - Fix file permissions if needed (legacy)
  clean                - Clean node_modules and build artifacts
  help                 - Show this help message

Examples:
  $0 build-webadmin               # Build WebAdmin
  $0 build-sdk admin           # Build Admin SDK only
  $0 lint-fix-webadmin           # Fix ESLint errors in WebAdmin
  $0 shell                     # Open shell in WebAdmin container
  $0 npm-install-webadmin         # Install WebAdmin dependencies
  $0 exec npm install axios    # Install a package
  $0 exec npm run test:unit   # Run specific test suite
  $0 install-and-build-local  # Fresh clone? Build everything locally

Environment Variables:
  DOCKER_COMPOSE_CMD   - Docker compose command (default: docker compose)

EOF
}

# Check if containers are running
check_containers() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    
    if ! $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" | grep -q "$WEBADMIN_SERVICE"; then
        log_error "WebAdmin container is not running. Start development environment first:"
        log_info "  $0 start-dev"
        exit 1
    fi
}

# Execute command in WebAdmin container
exec_in_webadmin() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_task "Executing in WebAdmin container: $*"
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml exec "$WEBADMIN_SERVICE" "$@"
}

# Build WebAdmin
build_webadmin() {
    log_info "Building WebAdmin in container's isolated .next directory..."
    log_warn "This production build is separate from host .next directory"
    exec_in_webadmin sh -c "cd /app/WebAdmin && npm run build"
    log_info "WebAdmin build completed (in container)"
}

# Build all SDKs
build_sdks() {
    log_info "Building all SDKs..."
    exec_in_webadmin sh -c "
        cd /app/SDKs/Node/Common && npm run build &&
        cd /app/SDKs/Node/Admin && npm run build &&
        cd /app/SDKs/Node/Core && npm run build
    "
    log_info "SDK builds completed"
}

# Build specific SDK
build_sdk() {
    local sdk_name="$1"
    local sdk_path=""
    
    case "$sdk_name" in
        common)
            sdk_path="Common"
            ;;
        admin)
            sdk_path="Admin"
            ;;
        core)
            sdk_path="Core"
            ;;
        *)
            log_error "Invalid SDK name: $sdk_name"
            log_info "Valid options: common, admin, core"
            exit 1
            ;;
    esac
    
    log_info "Building $sdk_name SDK..."
    exec_in_webadmin sh -c "cd /app/SDKs/Node/$sdk_path && npm run build"
    log_info "$sdk_name SDK build completed"
}

# Lint WebAdmin
lint_webadmin() {
    log_info "Running ESLint on WebAdmin..."
    exec_in_webadmin sh -c "cd /app/WebAdmin && npm run lint"
}

# Lint fix WebAdmin
lint_fix_webadmin() {
    log_info "Running ESLint with --fix on WebAdmin..."
    exec_in_webadmin sh -c "cd /app/WebAdmin && npm run lint:fix"
}

# Type check WebAdmin
type_check_webadmin() {
    log_info "Running TypeScript type checking on WebAdmin..."
    exec_in_webadmin sh -c "cd /app/WebAdmin && npm run type-check"
}

# Test WebAdmin
test_webadmin() {
    log_info "Running WebAdmin tests..."
    exec_in_webadmin sh -c "cd /app/WebAdmin && npm run test"
}

# Install WebAdmin dependencies
npm_install_webadmin() {
    log_info "Installing WebAdmin dependencies..."
    exec_in_webadmin sh -c "cd /app/WebAdmin && npm install"
}

# Install all SDK dependencies
npm_install_sdks() {
    log_info "Installing SDK dependencies..."
    exec_in_webadmin sh -c "
        cd /app/SDKs/Node/Common && npm install &&
        cd /app/SDKs/Node/Admin && npm install &&
        cd /app/SDKs/Node/Core && npm install
    "
}

# Open shell in WebAdmin container
open_shell() {
    log_info "Opening bash shell in WebAdmin container..."
    exec_in_webadmin bash
}

# Show WebAdmin logs
show_logs() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_info "Showing WebAdmin container logs..."
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml logs -f "$WEBADMIN_SERVICE"
}

# Restart WebAdmin container
restart_webadmin() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_info "Restarting WebAdmin container..."
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml restart "$WEBADMIN_SERVICE"
    log_info "WebAdmin container restarted"
}

# Show container status
show_status() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_info "Container status:"
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps
}

# Fix permissions (legacy - should not be needed with user mapping)
fix_permissions() {
    log_warn "This command is legacy and should not be needed with proper user mapping"
    log_info "Fixing file permissions..."

    # Fix ownership to current user (skip .next - container has its own isolated copy)
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/WebAdmin/node_modules" 2>/dev/null || true
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/WebAdmin/.next" 2>/dev/null || true
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/SDKs/Node/*/node_modules" 2>/dev/null || true
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/SDKs/Node/*/dist" 2>/dev/null || true

    log_info "Permissions fixed (note: container .next is isolated)"
}

# Clean build artifacts
clean() {
    log_info "Cleaning build artifacts..."

    # Remove node_modules and build outputs (host only - container has isolated .next)
    rm -rf "$PROJECT_ROOT/WebAdmin/node_modules"
    rm -rf "$PROJECT_ROOT/WebAdmin/.next"  # Host .next only
    rm -rf "$PROJECT_ROOT/SDKs/Node/Common/node_modules"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Common/dist"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Admin/node_modules"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Admin/dist"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Core/node_modules"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Core/dist"

    log_info "Clean completed (container .next is preserved)"
}

# Install dependencies for all TypeScript projects locally
install_local() {
    log_info "Installing dependencies for all TypeScript projects locally..."
    
    # Install Common SDK dependencies (no dependencies on other SDKs)
    log_task "Installing Common SDK dependencies..."
    cd "$PROJECT_ROOT/SDKs/Node/Common"
    npm install
    
    # Install Core SDK dependencies (depends on Common)
    log_task "Installing Core SDK dependencies..."
    cd "$PROJECT_ROOT/SDKs/Node/Core"
    npm install
    
    # Install Admin SDK dependencies (depends on Common)
    log_task "Installing Admin SDK dependencies..."
    cd "$PROJECT_ROOT/SDKs/Node/Admin"
    npm install
    
    # Install WebAdmin dependencies (depends on all SDKs via symlinks)
    log_task "Installing WebAdmin dependencies..."
    cd "$PROJECT_ROOT/WebAdmin"
    npm install
    
    log_info "All dependencies installed successfully!"
}

# Build all TypeScript projects locally
build_local() {
    log_info "Building all TypeScript projects locally..."
    
    # Build Common SDK first (base dependency)
    log_task "Building Common SDK..."
    cd "$PROJECT_ROOT/SDKs/Node/Common"
    npm run build
    
    # Build Core SDK (depends on Common)
    log_task "Building Core SDK..."
    cd "$PROJECT_ROOT/SDKs/Node/Core"
    npm run build
    
    # Build Admin SDK (depends on Common)
    log_task "Building Admin SDK..."
    cd "$PROJECT_ROOT/SDKs/Node/Admin"
    npm run build
    
    # Build WebAdmin (depends on all SDKs)
    log_task "Building WebAdmin..."
    cd "$PROJECT_ROOT/WebAdmin"
    npm run build
    
    log_info "All projects built successfully!"
}

# Install and build everything locally (for fresh clones)
install_and_build_local() {
    log_info "Installing and building all TypeScript projects locally..."
    log_warn "This is intended for fresh clones or CI environments"

    # First install all dependencies
    install_local

    # Then build everything
    build_local

    log_info "Installation and build completed successfully!"
    log_info "The WebAdmin production build is in: $PROJECT_ROOT/WebAdmin/.next (host build)"
    log_warn "Note: Container has its own isolated .next directory when running in Docker"
}

# Main execution
main() {
    local command="${1:-}"
    
    if [[ -z "$command" ]]; then
        show_usage
        exit 1
    fi
    
    # Change to project root
    cd "$PROJECT_ROOT"
    
    case "$command" in
        build-webadmin)
            check_containers
            build_webadmin
            ;;
        build-sdks)
            check_containers
            build_sdks
            ;;
        build-sdk)
            if [[ -z "${2:-}" ]]; then
                log_error "SDK name required"
                log_info "Usage: $0 build-sdk <common|admin|core>"
                exit 1
            fi
            check_containers
            build_sdk "$2"
            ;;
        lint-webadmin)
            check_containers
            lint_webadmin
            ;;
        lint-fix-webadmin)
            check_containers
            lint_fix_webadmin
            ;;
        type-check-webadmin)
            check_containers
            type_check_webadmin
            ;;
        test-webadmin)
            check_containers
            test_webadmin
            ;;
        npm-install-webadmin)
            check_containers
            npm_install_webadmin
            ;;
        npm-install-sdks)
            check_containers
            npm_install_sdks
            ;;
        shell)
            check_containers
            open_shell
            ;;
        logs)
            check_containers
            show_logs
            ;;
        restart-webadmin)
            restart_webadmin
            ;;
        status)
            show_status
            ;;
        fix-permissions)
            fix_permissions
            ;;
        clean)
            clean
            ;;
        install-local)
            install_local
            ;;
        build-local)
            build_local
            ;;
        install-and-build-local)
            install_and_build_local
            ;;
        exec)
            shift  # Remove 'exec' from arguments
            if [[ $# -eq 0 ]]; then
                log_error "No command provided to exec"
                log_info "Usage: $0 exec <command>"
                exit 1
            fi
            check_containers
            exec_in_webadmin "$@"
            ;;
        help|--help|-h)
            show_usage
            ;;
        *)
            log_error "Unknown command: $command"
            show_usage
            exit 1
            ;;
    esac
}

# Run main function
main "$@"