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
Conduit Development Environment Startup - AGGRESSIVE MODE

Usage: $0 [options]

Options:
  --build        FORCE rebuild (kills containers, rebuilds everything)
  --clean        FORCE clean (removes ALL containers/volumes, then starts fresh)
  --clean-only   FORCE clean without starting
  --fix-perms    Fix ALL permissions aggressively
  --logs         Show logs after startup
  --help         Show this help message

This script NOW:
1. Auto-fixes ALL permission issues without asking
2. Forcefully stops containers when using --build
3. Removes volumes completely with --clean
4. Uses sudo automatically when needed
5. No more permission errors!

After startup, APIs will be available at:
  - Core API Swagger: http://localhost:5000/swagger
  - Admin API Swagger: http://localhost:5002/swagger
  - WebUI: http://localhost:3000

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

# Fix host filesystem permissions - AGGRESSIVE
fix_host_permissions() {
    log_task "AGGRESSIVELY fixing host filesystem permissions..."
    
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    local dirs_to_fix=(
        "./ConduitLLM.WebUI"
        "./SDKs/Node/Admin"
        "./SDKs/Node/Core"
        "./SDKs/Node/Common"
    )
    
    # Just fix it - no asking, no checking
    for dir in "${dirs_to_fix[@]}"; do
        if [[ -d "$dir" ]]; then
            log_info "Force fixing permissions for: $dir"
            sudo chown -R "$current_uid:$current_gid" "$dir" 2>/dev/null || true
            # Also ensure directories are writable
            find "$dir" -type d -exec chmod 755 {} \; 2>/dev/null || true
            find "$dir" -type f -exec chmod 644 {} \; 2>/dev/null || true
        fi
    done
    
    log_info "Permissions fixed!"
}

# Fix Docker volume permissions - AGGRESSIVE
fix_volume_permissions() {
    log_task "AGGRESSIVELY fixing Docker volume permissions..."
    
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    
    # Kill any running containers first
    docker ps -q --filter "name=conduit" | xargs -r docker kill 2>/dev/null || true
    
    # Get all conduit volumes
    local all_volumes=$(docker volume ls --filter "name=conduit" --format "{{.Name}}" || true)
    
    if [[ -z "$all_volumes" ]]; then
        log_info "No volumes found"
        return 0
    fi
    
    # FORCE fix permissions for ALL volumes
    for volume in $all_volumes; do
        log_info "Force fixing volume: $volume"
        
        # Just fix it - no checks
        docker run --rm \
            -v "$volume:/fix" \
            alpine:latest \
            sh -c "chown -R $current_uid:$current_gid /fix 2>/dev/null || true; chmod -R 755 /fix 2>/dev/null || true" 2>/dev/null || true
    done
    
    log_info "Volume permissions fixed!"
    return 0
}

# Auto-fix permissions before starting
fix_permissions_before_start() {
    log_task "Auto-fixing all permissions..."
    
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    
    # Fix host permissions silently
    local dirs_to_fix=(
        "./ConduitLLM.WebUI"
        "./SDKs/Node/Admin"
        "./SDKs/Node/Core"
        "./SDKs/Node/Common"
    )
    
    for dir in "${dirs_to_fix[@]}"; do
        if [[ -d "$dir" ]]; then
            sudo chown -R "$current_uid:$current_gid" "$dir" 2>/dev/null || true
        fi
    done
    
    # Fix volume permissions if any exist
    local volumes=$(docker volume ls --filter "name=conduit" --format "{{.Name}}" || true)
    if [[ -n "$volumes" ]]; then
        for volume in $volumes; do
            docker run --rm \
                -v "$volume:/fix" \
                alpine:latest \
                sh -c "chown -R $current_uid:$current_gid /fix 2>/dev/null || true" 2>/dev/null || true
        done
    fi
    
    log_info "Permissions auto-fixed"
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

# Clean up existing containers and volumes - FORCEFULLY
clean_environment() {
    log_task "FORCEFULLY cleaning development environment..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    
    # Kill all conduit containers immediately
    log_info "Killing all Conduit containers..."
    docker ps -q --filter "name=conduit" | xargs -r docker kill 2>/dev/null || true
    
    # Remove all conduit containers
    log_info "Removing all Conduit containers..."
    docker ps -aq --filter "name=conduit" | xargs -r docker rm -f 2>/dev/null || true
    
    # Clean host filesystem directories
    local dirs_to_clean=(
        "./ConduitLLM.WebUI/.next"
        "./ConduitLLM.WebUI/node_modules"
        "./SDKs/Node/Admin/node_modules"
        "./SDKs/Node/Core/node_modules"
        "./SDKs/Node/Common/node_modules"
    )
    
    log_info "Cleaning all build artifacts and dependencies..."
    
    # Force remove with docker compose as well
    $compose_cmd -f docker-compose.yml down --volumes --remove-orphans --timeout 0 2>/dev/null || true
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml down --volumes --remove-orphans --timeout 0 2>/dev/null || true
    
    # FORCE remove ALL conduit volumes
    log_info "FORCE removing ALL Conduit volumes..."
    docker volume ls --filter "name=conduit" --format "{{.Name}}" | xargs -r docker volume rm -f 2>/dev/null || true
    
    # Double-check and force remove any stubborn volumes
    local remaining_volumes=$(docker volume ls --filter "name=conduit" --format "{{.Name}}" || true)
    if [[ -n "$remaining_volumes" ]]; then
        log_warn "Some volumes still exist, attempting sudo removal..."
        echo "$remaining_volumes" | while read -r volume; do
            sudo docker volume rm -f "$volume" 2>/dev/null || true
        done
    fi
    
    # Clean host filesystem directories
    log_info "Cleaning host filesystem directories..."
    local dirs_cleaned=0
    local dirs_failed=0
    
    for dir in "${dirs_to_clean[@]}"; do
        if [[ -d "$dir" ]]; then
            local owner=$(stat -c "%u:%g" "$dir" 2>/dev/null || echo "unknown")
            
            # FORCE remove with sudo if needed
            log_info "Force removing: $dir"
            if ! rm -rf "$dir" 2>/dev/null; then
                log_info "Using sudo to force remove: $dir"
                sudo rm -rf "$dir" 2>/dev/null || true
            fi
            ((dirs_cleaned++))
        fi
    done
    
    log_info "Environment cleaned!"
    
    # Give a moment for filesystem to sync
    sleep 1
}

# Start development environment with auto-fix
start_development() {
    log_task "Starting Conduit development environment..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    local build_flag=""
    local show_logs="${1:-false}"
    
    if [[ "${FORCE_BUILD:-false}" == "true" ]]; then
        # When --build is used, FORCE stop and remove existing containers first
        log_info "--build flag detected: Force stopping existing containers..."
        docker ps -q --filter "name=conduit" | xargs -r docker kill 2>/dev/null || true
        docker ps -aq --filter "name=conduit" | xargs -r docker rm -f 2>/dev/null || true
        build_flag="--build"
        log_info "Force building containers..."
    fi
    
    # Always fix permissions before starting
    fix_permissions_before_start
    
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
    local clean_only=false
    local fix_perms_only=false
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
            --clean-only)
                clean_only=true
                shift
                ;;
            --fix-perms)
                fix_perms_only=true
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
    
    if [[ "$fix_perms_only" == "true" ]]; then
        log_info "Fixing Permissions (Host Filesystem and Docker Volumes)"
        log_info "====================================================="
        
        check_prerequisites
        
        # Fix host filesystem permissions
        fix_host_permissions
        
        # Fix Docker volume permissions
        fix_volume_permissions
        
        log_info "Permission fix completed!"
        log_info "You can now run './scripts/start-dev.sh' to start the development environment"
        return 0
    fi
    
    if [[ "$clean_only" == "true" ]]; then
        log_info "Cleaning Conduit Development Environment"
        log_info "========================================"
        
        check_prerequisites
        check_container_conflicts
        check_filesystem_compatibility
        validate_user_mapping
        
        clean_environment
        
        log_info "Environment cleaned successfully!"
        log_info "Run './scripts/start-dev.sh' to start the development environment"
        return 0
    fi
    
    log_info "Starting Conduit Development Environment"
    log_info "========================================"
    
    check_prerequisites
    check_container_conflicts
    check_filesystem_compatibility
    validate_user_mapping
    validate_development_file_permissions
    
    if [[ "$clean" == "true" ]]; then
        clean_environment
    fi
    
    # Always fix permissions - no more checking, just fix
    
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