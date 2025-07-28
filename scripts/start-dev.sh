#!/usr/bin/env bash
# =============================================================================
# Conduit Development Environment Startup Script
# =============================================================================
# Canonical way to start the Conduit development environment with proper
# permissions and user mapping to avoid Docker permission issues.
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
Conduit Development Environment Startup

Usage: $0 [options]

Options:
  --build      Force rebuild of containers
  --clean      Clean existing containers and volumes
  --logs       Show logs after startup
  --help       Show this help message

This script:
1. Sets proper user/group IDs for Docker containers
2. Starts the development environment with hot reloading
3. Enables Swagger UI for both Core and Admin APIs
4. Provides file permission-free development experience

After startup, APIs will be available at:
  - Core API Swagger: http://localhost:5000/swagger
  - Admin API Swagger: http://localhost:5002/swagger
  - WebUI: http://localhost:3000

Use the dev-workflow.sh script for common development tasks:
  - ./scripts/dev-workflow.sh build-webui
  - ./scripts/dev-workflow.sh lint-fix-webui
  - ./scripts/dev-workflow.sh shell

EOF
}

# Check prerequisites
check_prerequisites() {
    log_task "Checking prerequisites..."
    
    # Check if we're in the right directory
    if [[ ! -f "Conduit.sln" ]]; then
        log_error "This script must be run from the Conduit root directory"
        exit 1
    fi
    
    # Check if Docker is running
    if ! docker info >/dev/null 2>&1; then
        log_error "Docker is not running. Please start Docker first."
        exit 1
    fi
    
    # Check if docker-compose files exist
    if [[ ! -f "docker-compose.yml" ]]; then
        log_error "docker-compose.yml not found"
        exit 1
    fi
    
    if [[ ! -f "docker-compose.dev.yml" ]]; then
        log_error "docker-compose.dev.yml not found"
        exit 1
    fi
    
    log_info "Prerequisites check passed"
}

# Set up environment variables for proper user mapping
setup_environment() {
    log_task "Setting up Docker user mapping..."
    
    # Export user/group IDs for Docker containers
    export DOCKER_USER_ID=$(id -u)
    export DOCKER_GROUP_ID=$(id -g)
    
    log_info "User ID: $DOCKER_USER_ID"
    log_info "Group ID: $DOCKER_GROUP_ID"
    
    # Ensure environment variables are available to docker-compose
    echo "DOCKER_USER_ID=$DOCKER_USER_ID" > .env.dev
    echo "DOCKER_GROUP_ID=$DOCKER_GROUP_ID" >> .env.dev
    
    log_info "Environment setup completed"
}

# Clean up existing containers and volumes
clean_environment() {
    log_task "Cleaning existing containers and volumes..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    
    # Stop and remove containers
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml down --volumes --remove-orphans
    
    # Remove development volumes
    docker volume rm -f conduit_webui_node_modules 2>/dev/null || true
    docker volume rm -f conduit_webui_next 2>/dev/null || true
    docker volume rm -f conduit_admin_sdk_node_modules 2>/dev/null || true
    docker volume rm -f conduit_core_sdk_node_modules 2>/dev/null || true
    docker volume rm -f conduit_common_sdk_node_modules 2>/dev/null || true
    
    log_info "Environment cleaned"
}

# Start development environment
start_development() {
    log_task "Starting Conduit development environment..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    local build_flag=""
    local show_logs="${1:-false}"
    
    if [[ "${FORCE_BUILD:-false}" == "true" ]]; then
        build_flag="--build"
        log_info "Force building containers..."
    fi
    
    # Start services
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml up -d $build_flag
    
    log_info "Development environment started successfully!"
    echo
    log_info "Services available at:"
    log_info "  📚 Core API Swagger:    http://localhost:5000/swagger"
    log_info "  🔧 Admin API Swagger:   http://localhost:5002/swagger"  
    log_info "  🌐 WebUI:               http://localhost:3000"
    log_info "  🐰 RabbitMQ Management: http://localhost:15672 (conduit/conduitpass)"
    echo
    log_info "Development commands:"
    log_info "  ./scripts/dev-workflow.sh build-webui      # Build WebUI"
    log_info "  ./scripts/dev-workflow.sh lint-fix-webui   # Fix ESLint errors"
    log_info "  ./scripts/dev-workflow.sh shell            # Open container shell"
    log_info "  ./scripts/dev-workflow.sh logs             # Show WebUI logs"
    echo
    
    if [[ "$show_logs" == "true" ]]; then
        log_info "Showing container logs (press Ctrl+C to exit)..."
        $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml logs -f
    fi
}

# Check container health
check_health() {
    log_task "Checking container health..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    local max_attempts=30
    local attempt=1
    
    while [[ $attempt -le $max_attempts ]]; do
        local healthy_count=$($compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" | wc -l)
        
        if [[ $healthy_count -ge 4 ]]; then
            log_info "All services are running"
            return 0
        fi
        
        log_info "Waiting for services to start... ($attempt/$max_attempts)"
        sleep 2
        ((attempt++))
    done
    
    log_warn "Some services may not have started properly. Check with:"
    log_warn "  docker compose -f docker-compose.yml -f docker-compose.dev.yml ps"
}

# Main execution
main() {
    local force_build=false
    local clean=false
    local show_logs=false
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --build)
                force_build=true
                shift
                ;;
            --clean)
                clean=true
                shift
                ;;
            --logs)
                show_logs=true
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
    
    # Change to project root
    cd "$PROJECT_ROOT"
    
    # Export variables for docker-compose
    export FORCE_BUILD=$force_build
    
    log_info "Starting Conduit Development Environment"
    log_info "========================================"
    
    check_prerequisites
    setup_environment
    
    if [[ "$clean" == "true" ]]; then
        clean_environment
    fi
    
    start_development "$show_logs"
    check_health
    
    log_info "Development environment is ready!"
    log_info "Use './scripts/dev-workflow.sh help' for development commands"
}

# Run main function
main "$@"