#!/usr/bin/env bash
# =============================================================================
# Conduit Development Environment Startup Script
# =============================================================================
# Simple, focused script for starting development environment
# =============================================================================

set -euo pipefail

# Color codes for output
readonly GREEN='\033[0;32m'
readonly RED='\033[0;31m'
readonly YELLOW='\033[1;33m'
readonly NC='\033[0m' # No Color

# Configuration
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

show_usage() {
    cat << EOF
Conduit Development Environment Startup

Usage: $0 [options]

Options:
  --clean        Delete volumes for fresh experience
  --build        Rebuild containers with --no-cache
  --webui        Rebuild WebUI container (fixes Next.js issues)
  --help         Show this help

Default behavior:
  - Build local Docker containers
  - Start from docker-compose.dev.yml
  - Mount WebUI directory for rapid development

Services available after startup:
  - WebUI:            http://localhost:3000
  - Core API:         http://localhost:5000/swagger
  - Admin API:        http://localhost:5002/swagger
  - RabbitMQ:         http://localhost:15672 (conduit/conduitpass)
  - Media Storage:    Cloudflare R2 (configured via .env)

Environment Variables:
  CONDUIT_S3_PUBLIC_BASE_URL - Set public URL for R2 bucket access

EOF
}

check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if we're in the right directory
    if [[ ! -f "Conduit.sln" ]]; then
        log_error "This script must be run from the Conduit root directory"
        exit 1
    fi
    
    # Check if Docker is running
    if ! docker info >/dev/null 2>&1; then
        log_error "Docker is not running. Please start Docker."
        exit 1
    fi
    
    # Check if compose files exist
    if [[ ! -f "docker-compose.yml" ]] || [[ ! -f "docker-compose.dev.yml" ]]; then
        log_error "docker-compose files not found"
        exit 1
    fi
    
    log_info "Prerequisites check passed"
}

clean_volumes() {
    log_info "Cleaning volumes for fresh experience..."
    
    # Stop containers
    docker compose -f docker-compose.yml -f docker-compose.dev.yml down --volumes --remove-orphans 2>/dev/null || true
    
    # Remove all conduit volumes
    docker volume ls --filter "name=conduit" --format "{{.Name}}" | xargs -r docker volume rm -f 2>/dev/null || true
    
    # Clean local build artifacts
    rm -rf ./ConduitLLM.WebUI/.next 2>/dev/null || true
    rm -rf ./ConduitLLM.WebUI/node_modules 2>/dev/null || true
    rm -rf ./SDKs/Node/*/node_modules 2>/dev/null || true
    
    log_info "Volumes cleaned"
}

build_containers() {
    local build_flags="$1"
    log_info "Building containers..."
    
    # Set user mapping for volume permissions
    export DOCKER_USER_ID=$(id -u)
    export DOCKER_GROUP_ID=$(id -g)
    
    # In development, only build services that need building
    # WebUI uses node:22-alpine directly with volume mounts
    docker compose -f docker-compose.yml -f docker-compose.dev.yml build $build_flags api admin rabbitmq
    
    log_info "Containers built"
}

rebuild_webui() {
    log_info "Restarting WebUI container to fix Next.js issues..."
    
    # Stop and remove WebUI container
    docker compose -f docker-compose.yml -f docker-compose.dev.yml stop webui 2>/dev/null || true
    docker compose -f docker-compose.yml -f docker-compose.dev.yml rm -f webui 2>/dev/null || true
    
    # Clean Next.js build artifacts that can cause issues
    rm -rf ./ConduitLLM.WebUI/.next 2>/dev/null || true
    
    # Start WebUI (no build needed - uses node:22-alpine with volume mounts)
    export DOCKER_USER_ID=$(id -u)
    export DOCKER_GROUP_ID=$(id -g)
    
    docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d webui
    
    log_info "WebUI container restarted"
    log_info "WebUI available at: http://localhost:3000"
}

start_development() {
    log_info "Starting development environment..."
    
    # Set user mapping for volume permissions
    export DOCKER_USER_ID=$(id -u)
    export DOCKER_GROUP_ID=$(id -g)
    
    # Start all services
    docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
    
    # Wait a moment for containers to initialize
    sleep 5
    
    # Check if containers are running
    local running_containers=$(docker compose -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" | wc -l)
    if [[ $running_containers -lt 4 ]]; then
        log_warn "Some containers may not have started properly"
        log_info "Check status with: docker compose -f docker-compose.yml -f docker-compose.dev.yml ps"
    fi
    
    log_info "Development environment started!"
    echo
    log_info "Services available at:"
    log_info "  🌐 WebUI:            http://localhost:3000"
    log_info "  📚 Core API:         http://localhost:5000/swagger"
    log_info "  🔧 Admin API:        http://localhost:5002/swagger"
    log_info "  🐰 RabbitMQ:         http://localhost:15672 (conduit/conduitpass)"
    log_info "  📦 Media Storage:    Cloudflare R2"
    echo
    log_info "The WebUI directory is mounted for rapid development."
    log_info "Changes to files will be reflected automatically."
}

main() {
    local clean_volumes_flag=false
    local build_flag=""
    local webui_only=false
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --clean)
                clean_volumes_flag=true
                shift
                ;;
            --build)
                build_flag="--no-cache"
                shift
                ;;
            --webui)
                webui_only=true
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
    
    check_prerequisites
    
    # Handle WebUI-only rebuild
    if [[ "$webui_only" == "true" ]]; then
        rebuild_webui
        return 0
    fi
    
    # Clean volumes if requested
    if [[ "$clean_volumes_flag" == "true" ]]; then
        clean_volumes
    fi
    
    # Build containers
    build_containers "$build_flag"
    
    # Start development environment
    start_development
}

# Run main function
main "$@"