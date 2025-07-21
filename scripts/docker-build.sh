#!/usr/bin/env bash
# =============================================================================
# Docker Build Script for Conduit
# =============================================================================
# Production-ready build script with versioning, tagging, and multi-platform support
# Usage: ./scripts/docker-build.sh [service] [version]
# =============================================================================

set -euo pipefail

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly NC='\033[0m' # No Color

# Configuration
readonly REGISTRY="${DOCKER_REGISTRY:-}"
readonly NAMESPACE="${DOCKER_NAMESPACE:-conduit}"
readonly BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ')
readonly VCS_REF=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
readonly VERSION="${2:-latest}"

# Services
readonly SERVICES=("api" "admin" "webui" "rabbitmq")

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

show_usage() {
    cat << EOF
Usage: $0 [service] [version]

Services:
  api       - Build Conduit HTTP API service
  admin     - Build Conduit Admin API service  
  webui     - Build Conduit WebUI service
  rabbitmq  - Build custom RabbitMQ image
  all       - Build all services

Options:
  version   - Version tag (default: latest)

Environment Variables:
  DOCKER_REGISTRY   - Docker registry URL (optional)
  DOCKER_NAMESPACE  - Docker namespace (default: conduit)
  DOCKER_BUILDKIT   - Enable BuildKit (default: 1)
  DOCKER_PLATFORM   - Target platform (default: linux/amd64)

Examples:
  $0 api                    # Build API with 'latest' tag
  $0 webui v2.0.0          # Build WebUI with 'v2.0.0' tag
  $0 all v2.0.0            # Build all services with 'v2.0.0' tag

EOF
}

build_service() {
    local service=$1
    local dockerfile=""
    local context="."
    local image_name="${NAMESPACE}/${service}:${VERSION}"
    
    if [[ -n "${REGISTRY}" ]]; then
        image_name="${REGISTRY}/${image_name}"
    fi
    
    case $service in
        api)
            dockerfile="ConduitLLM.Http/Dockerfile"
            ;;
        admin)
            dockerfile="ConduitLLM.Admin/Dockerfile"
            ;;
        webui)
            dockerfile="ConduitLLM.WebUI/Dockerfile"
            ;;
        rabbitmq)
            dockerfile="docker/rabbitmq/Dockerfile"
            ;;
        *)
            log_error "Unknown service: $service"
            return 1
            ;;
    esac
    
    log_info "Building ${service} service..."
    log_info "Image: ${image_name}"
    log_info "Dockerfile: ${dockerfile}"
    log_info "Context: ${context}"
    
    # Enable BuildKit for better performance
    export DOCKER_BUILDKIT=1
    
    # Build with proper build arguments
    docker build \
        --build-arg BUILD_DATE="${BUILD_DATE}" \
        --build-arg VERSION="${VERSION}" \
        --build-arg VCS_REF="${VCS_REF}" \
        --platform "${DOCKER_PLATFORM:-linux/amd64}" \
        --tag "${image_name}" \
        --file "${dockerfile}" \
        "${context}"
    
    # Tag as latest if building a version
    if [[ "${VERSION}" != "latest" ]]; then
        local latest_tag="${NAMESPACE}/${service}:latest"
        if [[ -n "${REGISTRY}" ]]; then
            latest_tag="${REGISTRY}/${latest_tag}"
        fi
        docker tag "${image_name}" "${latest_tag}"
        log_info "Also tagged as: ${latest_tag}"
    fi
    
    log_info "Successfully built ${service}"
}

# Main execution
main() {
    local service="${1:-}"
    
    if [[ -z "$service" ]]; then
        show_usage
        exit 1
    fi
    
    if [[ "$service" == "help" || "$service" == "-h" || "$service" == "--help" ]]; then
        show_usage
        exit 0
    fi
    
    # Check if we're in the right directory
    if [[ ! -f "Conduit.sln" ]]; then
        log_error "This script must be run from the Conduit root directory"
        exit 1
    fi
    
    # Build service(s)
    if [[ "$service" == "all" ]]; then
        log_info "Building all services with version ${VERSION}"
        for svc in "${SERVICES[@]}"; do
            build_service "$svc"
        done
    else
        if [[ ! " ${SERVICES[@]} " =~ " ${service} " ]]; then
            log_error "Invalid service: $service"
            log_error "Valid services: ${SERVICES[*]}"
            exit 1
        fi
        build_service "$service"
    fi
    
    log_info "Build completed successfully!"
    
    # Show built images
    echo
    log_info "Built images:"
    docker images --filter "reference=${NAMESPACE}/*" --format "table {{.Repository}}:{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}"
}

# Run main function
main "$@"