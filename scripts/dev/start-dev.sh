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
readonly PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

# Cleanup function for error handling
cleanup_on_error() {
    log_error "Startup failed. Cleaning up partial state..."
    docker compose -f docker-compose.yml -f docker-compose.dev.yml down --remove-orphans 2>/dev/null || true
    exit 1
}

# Auto-cleanup stale Conduit containers
cleanup_stale_containers() {
    log_info "Checking for stale Conduit containers..."

    # Get all Conduit-related containers (running or stopped)
    local conduit_containers=$(docker ps -a --filter "name=conduit-" --format "{{.Names}}" 2>/dev/null || true)

    if [[ -n "$conduit_containers" ]]; then
        log_info "Found stale Conduit containers, cleaning up..."
        docker compose -f docker-compose.yml -f docker-compose.dev.yml down --remove-orphans 2>/dev/null || true
        log_info "Stale containers removed"
    fi
}

# Check for port conflicts before starting
check_port_conflicts() {
    log_info "Checking for port conflicts..."

    local ports=(6379 5432 5000 5002 3000 15672)
    local port_names=("Redis" "PostgreSQL" "Core API" "Admin API" "WebUI" "RabbitMQ")
    local conflicts_found=false
    local conflicting_containers=()

    for i in "${!ports[@]}"; do
        local port="${ports[$i]}"
        local name="${port_names[$i]}"

        # Check if port is in use
        if ss -tuln 2>/dev/null | grep -q ":$port " || lsof -i ":$port" >/dev/null 2>&1; then
            # Find what's using the port
            local container=$(docker ps --format "{{.Names}}" --filter "publish=$port" 2>/dev/null | head -1)

            if [[ -n "$container" ]]; then
                # Port is used by a Docker container
                if [[ "$container" == conduit-* ]]; then
                    log_warn "Port $port ($name) is used by stale Conduit container: $container"
                    # Will be cleaned up by cleanup_stale_containers
                else
                    log_warn "Port $port ($name) is used by container: $container"
                    conflicting_containers+=("$container")
                    conflicts_found=true
                fi
            else
                # Port is used by a system process
                log_warn "Port $port ($name) is in use by a system process"
                log_info "  Check with: sudo lsof -i :$port"
                conflicts_found=true
            fi
        fi
    done

    # Handle non-Conduit Docker container conflicts
    if [[ ${#conflicting_containers[@]} -gt 0 ]]; then
        echo
        log_warn "The following Docker containers are blocking required ports:"
        for container in "${conflicting_containers[@]}"; do
            echo "  - $container"
        done
        echo
        read -p "Stop these containers? [y/N] " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            for container in "${conflicting_containers[@]}"; do
                log_info "Stopping $container..."
                docker stop "$container" 2>/dev/null || true
            done
            log_info "Conflicting containers stopped"
        else
            log_error "Cannot proceed with port conflicts. Please resolve manually."
            exit 1
        fi
    elif [[ "$conflicts_found" == "true" ]]; then
        log_error "Port conflicts detected. Please resolve the system process conflicts and try again."
        exit 1
    fi

    log_info "No port conflicts detected"
}

show_usage() {
    cat << EOF
Conduit Development Environment Startup

Usage: $0 [options]

Options:
  --clean        Delete volumes for fresh experience
  --build        Rebuild containers (smart caching: keeps OS layers, rebuilds .NET code)
  --rebuild      Full rebuild with --no-cache (slower, use when --build fails)
  --webui        Rebuild WebUI container (fixes Next.js issues)
  --logs [service]  Show container logs (api|core|admin|rabbitmq|webui, or all if omitted)
  --help         Show this help

Default behavior:
  - Automatically cleans up stale Conduit containers
  - Checks for port conflicts (offers to stop conflicting containers)
  - Build local Docker containers
  - Start from docker-compose.dev.yml
  - Mount WebUI directory for rapid development

Services available after startup:
  - WebUI:            http://localhost:3000
  - Core API:         http://localhost:5000/scalar/v1
  - Admin API:        http://localhost:5002/scalar/v1
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

    # Clean local build artifacts (host only - container has isolated .next)
    rm -rf ./WebAdmin/.next 2>/dev/null || true
    rm -rf ./WebAdmin/node_modules 2>/dev/null || true
    rm -rf ./SDKs/Node/*/node_modules 2>/dev/null || true
    rm -rf ./SDKs/Node/*/dist 2>/dev/null || true

    log_info "Volumes cleaned"
}

build_containers() {
    local build_flags="$1"
    log_info "Building containers..."
    
    # Set user mapping for volume permissions
    export DOCKER_USER_ID=$(id -u)
    export DOCKER_GROUP_ID=$(id -g)
    
    # Generate timestamp to force .NET rebuild while keeping OS layers cached
    local cachebust=$(date +%s)
    log_info "Using CACHEBUST: $cachebust (forces .NET code rebuild, keeps OS layers cached)"
    
    # Build with CACHEBUST to force .NET layers to rebuild while preserving OS cache
    docker compose -f docker-compose.yml -f docker-compose.dev.yml build \
        --build-arg CACHEBUST=$cachebust \
        $build_flags \
        api admin rabbitmq
    
    log_info "Containers built (CACHEBUST: $cachebust)"
}

build_sdks() {
    log_info "Building SDK packages for WebUI..."
    
    # Check if SDKs need building
    if [[ ! -d "./SDKs/Node/Common/dist" ]] || [[ ! -d "./SDKs/Node/Core/dist" ]] || [[ ! -d "./SDKs/Node/Admin/dist" ]]; then
        log_info "SDK packages not built, building now..."
        
        # Build Common SDK first (dependency for others)
        if [[ -d "./SDKs/Node/Common" ]]; then
            log_info "Building Common SDK..."
            (cd ./SDKs/Node/Common && npm install && npm run build) || {
                log_error "Failed to build Common SDK"
                exit 1
            }
        fi
        
        # Build Core SDK
        if [[ -d "./SDKs/Node/Core" ]]; then
            log_info "Building Core SDK..."
            (cd ./SDKs/Node/Core && npm install && npm run build) || {
                log_error "Failed to build Core SDK"
                exit 1
            }
        fi
        
        # Build Admin SDK
        if [[ -d "./SDKs/Node/Admin" ]]; then
            log_info "Building Admin SDK..."
            (cd ./SDKs/Node/Admin && npm install && npm run build) || {
                log_error "Failed to build Admin SDK"
                exit 1
            }
        fi
        
        log_info "SDK packages built successfully"
    else
        log_info "SDK packages already built, skipping..."
    fi
}

rebuild_webui() {
    log_info "Restarting WebUI container to fix Next.js issues..."

    # Ensure SDKs are built (WebUI depends on them)
    build_sdks

    # Stop and remove WebUI container
    docker compose -f docker-compose.yml -f docker-compose.dev.yml stop webui 2>/dev/null || true
    docker compose -f docker-compose.yml -f docker-compose.dev.yml rm -f webui 2>/dev/null || true

    # Clean host's Next.js build artifacts (container has its own isolated .next)
    rm -rf ./WebAdmin/.next 2>/dev/null || true

    # Start WebUI (no build needed - uses node:22-alpine with volume mounts)
    export DOCKER_USER_ID=$(id -u)
    export DOCKER_GROUP_ID=$(id -g)

    docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d webui

    log_info "WebUI container restarted"
    log_info "WebUI available at: http://localhost:3000"
}

show_logs() {
    local service="$1"

    # Map "core" alias to "api"
    if [[ "$service" == "core" ]]; then
        service="api"
    fi

    # Validate service name if provided
    if [[ -n "$service" ]] && [[ ! "$service" =~ ^(api|admin|rabbitmq|webui)$ ]]; then
        log_error "Invalid service: $service"
        log_info "Valid services: api (or core), admin, rabbitmq, webui"
        exit 1
    fi

    # Show logs
    if [[ -z "$service" ]]; then
        log_info "Showing logs for all services (Ctrl+C to exit)..."
        docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f
    else
        log_info "Showing logs for $service (Ctrl+C to exit)..."
        docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f "$service"
    fi
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
    log_info "  📚 Core API:         http://localhost:5000/scalar/v1"
    log_info "  🔧 Admin API:        http://localhost:5002/scalar/v1"
    log_info "  🐰 RabbitMQ:         http://localhost:15672 (conduit/conduitpass)"
    log_info "  📦 Media Storage:    Cloudflare R2"
    echo
    log_info "The WebUI directory is mounted for rapid development."
    log_info "Changes to files will be reflected automatically."

    # Disable error trap after successful startup
    trap - ERR
}

main() {
    # Set up error trap to cleanup on failure
    trap cleanup_on_error ERR

    local clean_volumes_flag=false
    local build_flag=""
    local webui_only=false
    local show_logs_flag=false
    local logs_service=""

    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --clean)
                clean_volumes_flag=true
                shift
                ;;
            --build)
                build_flag=""  # Use cache where possible, CACHEBUST handles .NET invalidation
                shift
                ;;
            --rebuild)
                build_flag="--no-cache"  # Nuclear option for full rebuild
                shift
                ;;
            --webui)
                webui_only=true
                shift
                ;;
            --logs)
                show_logs_flag=true
                shift
                # Check if next argument is a service name (not another flag)
                if [[ $# -gt 0 ]] && [[ ! "$1" =~ ^-- ]]; then
                    logs_service="$1"
                    shift
                fi
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

    # Handle logs display
    if [[ "$show_logs_flag" == "true" ]]; then
        show_logs "$logs_service"
        return 0
    fi

    check_prerequisites

    # Handle WebUI-only rebuild
    if [[ "$webui_only" == "true" ]]; then
        rebuild_webui
        return 0
    fi

    # Auto-cleanup stale containers before starting
    cleanup_stale_containers

    # Check for port conflicts
    check_port_conflicts

    # Clean volumes if requested
    if [[ "$clean_volumes_flag" == "true" ]]; then
        clean_volumes
    fi

    # Build containers
    build_containers "$build_flag"

    # Build SDKs (required for WebUI)
    build_sdks

    # Start development environment
    start_development
}

# Run main function
main "$@"