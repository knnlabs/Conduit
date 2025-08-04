#!/usr/bin/env bash
# =============================================================================
# Conduit Development Workflow Script
# =============================================================================
# Provides convenient development commands for working with the WebUI and SDKs
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
readonly PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
readonly WEBUI_SERVICE="webui"

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

Development Commands:
  build-webui          - Build the WebUI application
  build-sdks           - Build all SDK packages (Common, Admin, Core)
  build-sdk <name>     - Build specific SDK (common|admin|core)
  lint-webui           - Run ESLint on WebUI
  lint-fix-webui       - Run ESLint with --fix on WebUI
  type-check-webui     - Run TypeScript type checking on WebUI
  test-webui           - Run WebUI tests
  npm-install-webui    - Install WebUI dependencies
  npm-install-sdks     - Install all SDK dependencies
  shell                - Open bash shell in WebUI container
  logs                 - Show WebUI container logs
  restart-webui        - Restart WebUI container
  status               - Show container status
  exec <cmd>           - Execute any command in WebUI container

Utility Commands:
  fix-permissions      - Fix file permissions if needed (legacy)
  clean                - Clean node_modules and build artifacts
  help                 - Show this help message

Examples:
  $0 build-webui               # Build WebUI
  $0 build-sdk admin           # Build Admin SDK only
  $0 lint-fix-webui           # Fix ESLint errors in WebUI
  $0 shell                     # Open shell in WebUI container
  $0 npm-install-webui         # Install WebUI dependencies
  $0 exec npm install axios    # Install a package
  $0 exec npm run test:unit   # Run specific test suite

Environment Variables:
  DOCKER_COMPOSE_CMD   - Docker compose command (default: docker compose)

EOF
}

# Check if containers are running
check_containers() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    
    if ! $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" | grep -q "$WEBUI_SERVICE"; then
        log_error "WebUI container is not running. Start development environment first:"
        log_info "  $0 start-dev"
        exit 1
    fi
}

# Execute command in WebUI container
exec_in_webui() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_task "Executing in WebUI container: $*"
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml exec "$WEBUI_SERVICE" "$@"
}

# Build WebUI
build_webui() {
    log_info "Building WebUI..."
    exec_in_webui sh -c "cd /app/ConduitLLM.WebUI && npm run build"
    log_info "WebUI build completed"
}

# Build all SDKs
build_sdks() {
    log_info "Building all SDKs..."
    exec_in_webui sh -c "
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
    exec_in_webui sh -c "cd /app/SDKs/Node/$sdk_path && npm run build"
    log_info "$sdk_name SDK build completed"
}

# Lint WebUI
lint_webui() {
    log_info "Running ESLint on WebUI..."
    exec_in_webui sh -c "cd /app/ConduitLLM.WebUI && npm run lint"
}

# Lint fix WebUI
lint_fix_webui() {
    log_info "Running ESLint with --fix on WebUI..."
    exec_in_webui sh -c "cd /app/ConduitLLM.WebUI && npm run lint:fix"
}

# Type check WebUI
type_check_webui() {
    log_info "Running TypeScript type checking on WebUI..."
    exec_in_webui sh -c "cd /app/ConduitLLM.WebUI && npm run type-check"
}

# Test WebUI
test_webui() {
    log_info "Running WebUI tests..."
    exec_in_webui sh -c "cd /app/ConduitLLM.WebUI && npm run test"
}

# Install WebUI dependencies
npm_install_webui() {
    log_info "Installing WebUI dependencies..."
    exec_in_webui sh -c "cd /app/ConduitLLM.WebUI && npm install"
}

# Install all SDK dependencies
npm_install_sdks() {
    log_info "Installing SDK dependencies..."
    exec_in_webui sh -c "
        cd /app/SDKs/Node/Common && npm install &&
        cd /app/SDKs/Node/Admin && npm install &&
        cd /app/SDKs/Node/Core && npm install
    "
}

# Open shell in WebUI container
open_shell() {
    log_info "Opening bash shell in WebUI container..."
    exec_in_webui bash
}

# Show WebUI logs
show_logs() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_info "Showing WebUI container logs..."
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml logs -f "$WEBUI_SERVICE"
}

# Restart WebUI container
restart_webui() {
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    log_info "Restarting WebUI container..."
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml restart "$WEBUI_SERVICE"
    log_info "WebUI container restarted"
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
    
    # Fix ownership to current user
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/ConduitLLM.WebUI/node_modules" 2>/dev/null || true
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/ConduitLLM.WebUI/.next" 2>/dev/null || true
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/SDKs/Node/*/node_modules" 2>/dev/null || true
    sudo chown -R "$(id -u):$(id -g)" "$PROJECT_ROOT/SDKs/Node/*/dist" 2>/dev/null || true
    
    log_info "Permissions fixed"
}

# Clean build artifacts
clean() {
    log_info "Cleaning build artifacts..."
    
    # Remove node_modules and build outputs
    rm -rf "$PROJECT_ROOT/ConduitLLM.WebUI/node_modules"
    rm -rf "$PROJECT_ROOT/ConduitLLM.WebUI/.next"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Common/node_modules"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Common/dist"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Admin/node_modules" 
    rm -rf "$PROJECT_ROOT/SDKs/Node/Admin/dist"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Core/node_modules"
    rm -rf "$PROJECT_ROOT/SDKs/Node/Core/dist"
    
    log_info "Clean completed"
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
        build-webui)
            check_containers
            build_webui
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
        lint-webui)
            check_containers
            lint_webui
            ;;
        lint-fix-webui)
            check_containers
            lint_fix_webui
            ;;
        type-check-webui)
            check_containers
            type_check_webui
            ;;
        test-webui)
            check_containers
            test_webui
            ;;
        npm-install-webui)
            check_containers
            npm_install_webui
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
        restart-webui)
            restart_webui
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
        exec)
            shift  # Remove 'exec' from arguments
            if [[ $# -eq 0 ]]; then
                log_error "No command provided to exec"
                log_info "Usage: $0 exec <command>"
                exit 1
            fi
            check_containers
            exec_in_webui "$@"
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