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

# Check for container conflicts between production and development setups
check_container_conflicts() {
    log_task "Checking for container conflicts..."
    
    # Look for WebUI containers using production image (not node:20-alpine)
    local webui_containers=$(docker ps -a --filter "name=conduit-webui" --format "{{.Names}}\t{{.Image}}" || true)
    
    if [[ -n "$webui_containers" ]]; then
        # Check if any WebUI containers are using production images
        local prod_webui=$(echo "$webui_containers" | grep -v "node:20-alpine" || true)
        
        if [[ -n "$prod_webui" ]]; then
            log_error "Found production WebUI containers that conflict with development setup:"
            echo
            echo "$prod_webui"
            echo
            log_error "These containers were created with 'docker compose up' (production build)."
            log_error "To fix this, run:"
            log_error "  docker compose down --volumes --remove-orphans"
            log_error "Then re-run this script."
            exit 1
        fi
        
        # If WebUI containers exist and are using development image, just warn we'll recreate
        local dev_webui=$(echo "$webui_containers" | grep "node:20-alpine" || true)
        if [[ -n "$dev_webui" ]]; then
            log_warn "Found existing development WebUI containers:"
            echo "$dev_webui"
            log_warn "These will be stopped and recreated."
        fi
    fi
    
    log_info "Container conflict check passed"
}

# Check volume permissions and ownership
check_volume_permissions() {
    log_task "Checking volume permissions..."
    
    # Get current user info
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    
    # Check if problematic volumes exist from previous production runs
    local problematic_volumes=$(docker volume ls --filter "name=conduit" --format "{{.Name}}" | grep -v "conduit.*dev" || true)
    
    if [[ -n "$problematic_volumes" ]]; then
        log_warn "Found volumes from production setup:"
        echo "$problematic_volumes" | while read -r volume; do
            echo "  - $volume"
        done
        echo
        
        # Test if we can write to these volumes with our user mapping
        local volume_test_failed=false
        for volume in $problematic_volumes; do
            if echo "$volume" | grep -q "node_modules\|webui\|next"; then
                log_info "Testing write access to volume: $volume"
                if ! docker run --rm -v "$volume:/test" -u "$current_uid:$current_gid" alpine:latest sh -c "touch /test/.write_test 2>/dev/null && rm /test/.write_test 2>/dev/null" >/dev/null 2>&1; then
                    log_error "Cannot write to volume '$volume' with user $current_uid:$current_gid"
                    volume_test_failed=true
                fi
            fi
        done
        
        if [[ "$volume_test_failed" == "true" ]]; then
            log_error "Volume permission mismatch detected."
            log_error "These volumes have incompatible permissions from production containers."
            log_error "To fix this, run:"
            log_error "  ./scripts/start-dev.sh --clean"
            log_error "This will remove the problematic volumes and recreate them with correct permissions."
            exit 1
        fi
    fi
    
    log_info "Volume permissions check passed"
}

# Check filesystem compatibility
check_filesystem_compatibility() {
    log_task "Checking filesystem compatibility..."
    
    # Check for SELinux
    if command -v getenforce >/dev/null 2>&1; then
        local selinux_status=$(getenforce 2>/dev/null || echo "unknown")
        if [[ "$selinux_status" == "Enforcing" ]]; then
            log_warn "SELinux detected in enforcing mode"
            log_warn "If you encounter permission issues, you may need to:"
            log_warn "  - Add :Z flag to volume mounts in docker-compose.dev.yml"
            log_warn "  - Or run: sudo setsebool -P container_manage_cgroup 1"
        fi
    fi
    
    # Check for AppArmor
    if command -v aa-status >/dev/null 2>&1 && aa-status >/dev/null 2>&1; then
        log_warn "AppArmor detected - may cause permission issues with volume mounts"
    fi
    
    # Check filesystem type for WebUI directory
    local fs_type=$(stat -f -c %T ./ConduitLLM.WebUI 2>/dev/null || echo "unknown")
    case "$fs_type" in
        "nfs"|"cifs"|"fuse"*)
            log_warn "Network/FUSE filesystem detected: $fs_type"
            log_warn "Volume mounting may have permission issues"
            log_warn "Consider running containers without volume mounts if issues occur"
            ;;
    esac
    
    log_info "Filesystem compatibility check completed"
}

# Validate user mapping and Docker permissions
validate_user_mapping() {
    log_task "Validating Docker user mapping..."
    
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    
    # Ensure the user can run Docker commands
    if ! docker run --rm alpine:latest echo "Docker access test" >/dev/null 2>&1; then
        log_error "Cannot run Docker containers."
        log_error "You may need to add your user to the docker group:"
        log_error "  sudo usermod -aG docker $USER"
        log_error "  newgrp docker"
        log_error "Then restart your terminal and try again."
        exit 1
    fi
    
    # Test that user mapping will work
    local mapped_uid=$(docker run --rm -u "$current_uid:$current_gid" alpine:latest id -u 2>/dev/null || echo "failed")
    if [[ "$mapped_uid" != "$current_uid" ]]; then
        log_error "Docker user mapping test failed"
        log_error "Expected UID: $current_uid, Got: $mapped_uid"
        log_error "This system may not support user namespace mapping"
        exit 1
    fi
    
    log_info "User mapping validation passed (UID: $current_uid, GID: $current_gid)"
}

# Test comprehensive file permissions for development workflow
validate_development_file_permissions() {
    log_task "Testing development file permissions..."
    
    local temp_test_file="./ConduitLLM.WebUI/.dev-permission-test-$$"
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    
    # Test 1: Host file creation and editing
    if ! touch "$temp_test_file" 2>/dev/null; then
        log_error "Cannot create files in WebUI directory on host"
        log_error "Check permissions: ls -la ./ConduitLLM.WebUI/"
        log_error "You may need to run: sudo chown -R $USER:$USER ./ConduitLLM.WebUI ./SDKs"
        exit 1
    fi
    
    echo "host-write-test" > "$temp_test_file"
    
    # Test 2: Verify file ownership
    local file_owner=$(stat -c "%u:%g" "$temp_test_file" 2>/dev/null || echo "unknown")
    if [[ "$file_owner" != "$current_uid:$current_gid" ]]; then
        log_warn "File ownership mismatch: expected $current_uid:$current_gid, got $file_owner"
    fi
    
    log_info "Host file permission test passed"
    
    # Cleanup
    rm -f "$temp_test_file"
}

# Test that development environment works end-to-end (run after container start)
validate_full_development_stack() {
    log_task "Validating complete development workflow..."
    
    local max_attempts=30
    local attempt=1
    local temp_test_file="./ConduitLLM.WebUI/.dev-workflow-test-$$"
    
    # Wait for WebUI container to be ready
    while [[ $attempt -le $max_attempts ]]; do
        if docker exec conduit-webui-1 echo "Container ready" >/dev/null 2>&1; then
            break
        fi
        
        if [[ $attempt -eq $max_attempts ]]; then
            log_error "WebUI container not responding after $max_attempts attempts"
            log_error "Check container status: docker ps"
            log_error "Check container logs: docker logs conduit-webui-1"
            return 1
        fi
        
        log_info "Waiting for WebUI container... ($attempt/$max_attempts)"
        sleep 2
        ((attempt++))
    done
    
    # Test 1: Host creates file, container can read it
    echo "host-created-content" > "$temp_test_file"
    local container_read_result=$(docker exec conduit-webui-1 cat "/app/ConduitLLM.WebUI/.dev-workflow-test-$$" 2>/dev/null || echo "failed")
    if [[ "$container_read_result" != "host-created-content" ]]; then
        log_error "Container cannot read host-created files"
        log_error "Volume mounting may have failed"
        rm -f "$temp_test_file"
        return 1
    fi
    
    # Test 2: Container modifies file, host can read it
    docker exec conduit-webui-1 sh -c "echo 'container-modified-content' >> /app/ConduitLLM.WebUI/.dev-workflow-test-$$" 2>/dev/null
    if ! grep -q "container-modified-content" "$temp_test_file" 2>/dev/null; then
        log_error "Host cannot read container-modified files"
        log_error "Volume mounting may have permission issues"
        rm -f "$temp_test_file"
        return 1
    fi
    
    # Test 3: npm operations work in container
    if ! docker exec conduit-webui-1 npm --version >/dev/null 2>&1; then
        log_error "npm not working in container"
        rm -f "$temp_test_file"
        return 1
    fi
    
    # Test 4: Node.js file watching capabilities (basic test)
    if ! docker exec conduit-webui-1 test -d "/app/ConduitLLM.WebUI/node_modules" 2>/dev/null; then
        log_warn "node_modules not found - dependency installation may have failed"
        log_warn "Check container logs: docker logs conduit-webui-1"
    fi
    
    # Cleanup
    rm -f "$temp_test_file"
    docker exec conduit-webui-1 rm -f "/app/ConduitLLM.WebUI/.dev-workflow-test-$$" 2>/dev/null || true
    
    log_info "Development workflow validation completed successfully"
    return 0
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

# Clean up existing containers and volumes with safety checks
clean_environment() {
    log_task "Cleaning development environment..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    
    # Show what will be cleaned before proceeding
    log_info "This will clean the following:"
    
    # Check for running containers
    local running_containers=$(docker ps --filter "name=conduit" --format "{{.Names}}" || true)
    if [[ -n "$running_containers" ]]; then
        log_info "Running containers:"
        echo "$running_containers" | while read -r container; do
            echo "  - $container"
        done
    fi
    
    # Check for existing volumes
    local existing_volumes=$(docker volume ls --filter "name=conduit" --format "{{.Name}}" || true)
    if [[ -n "$existing_volumes" ]]; then
        log_info "Volumes to remove:"
        echo "$existing_volumes" | while read -r volume; do
            echo "  - $volume"
        done
    fi
    
    echo
    log_warn "This will stop all Conduit containers and remove development volumes"
    log_warn "Your source code will NOT be affected, only Docker containers and volumes"
    echo
    
    # Stop and remove containers (both production and development)
    log_info "Stopping containers..."
    $compose_cmd -f docker-compose.yml down --remove-orphans 2>/dev/null || true
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml down --volumes --remove-orphans 2>/dev/null || true
    
    # Remove specific development volumes with better error handling
    local dev_volumes=(
        "conduit_webui_node_modules"
        "conduit_webui_next"
        "conduit_admin_sdk_node_modules" 
        "conduit_core_sdk_node_modules"
        "conduit_common_sdk_node_modules"
        # Also clean volumes with dev prefix if they exist
        "conduit-dev-webui-node-modules"
        "conduit-dev-webui-next"
        "conduit-dev-admin-sdk-node-modules"
        "conduit-dev-core-sdk-node-modules" 
        "conduit-dev-common-sdk-node-modules"
    )
    
    log_info "Removing development volumes..."
    local volumes_removed=0
    local volumes_failed=0
    
    for volume in "${dev_volumes[@]}"; do
        if docker volume inspect "$volume" >/dev/null 2>&1; then
            log_info "Removing volume: $volume"
            if docker volume rm "$volume" >/dev/null 2>&1; then
                ((volumes_removed++))
            else
                log_error "Failed to remove volume: $volume"
                log_error "You may need to run: sudo docker volume rm $volume"
                ((volumes_failed++))
            fi
        fi
    done
    
    # Clean up any orphaned volumes that match our patterns
    local orphaned_volumes=$(docker volume ls --filter "name=conduit" --format "{{.Name}}" | grep -E "(node_modules|webui|next)" || true)
    if [[ -n "$orphaned_volumes" ]]; then
        log_info "Found additional volumes to clean:"
        echo "$orphaned_volumes" | while read -r volume; do
            log_info "Removing orphaned volume: $volume"
            docker volume rm "$volume" 2>/dev/null || log_warn "Could not remove: $volume"
        done
    fi
    
    # Report results
    if [[ $volumes_failed -eq 0 ]]; then
        log_info "Environment cleaned successfully"
        log_info "Removed $volumes_removed volumes"
    else
        log_warn "Environment partially cleaned"
        log_warn "Removed: $volumes_removed volumes, Failed: $volumes_failed volumes"
        log_warn "You may need to manually remove failed volumes with sudo"
    fi
    
    # Give a moment for Docker to clean up
    sleep 2
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
    check_container_conflicts
    check_filesystem_compatibility
    validate_user_mapping
    validate_development_file_permissions
    
    if [[ "$clean" == "true" ]]; then
        clean_environment
    else
        # Only check volume permissions if we're not cleaning
        check_volume_permissions
    fi
    
    setup_environment
    
    start_development "$show_logs"
    check_health
    
    # Post-startup validation
    if validate_full_development_stack; then
        log_info "Development environment validation completed successfully"
    else
        log_warn "Development environment validation had issues"
        log_warn "Check container logs: docker logs conduit-webui-1"
    fi
    
    log_info "Development environment is ready!"
    log_info "Use './scripts/dev-workflow.sh help' for development commands"
}

# Run main function
main "$@"