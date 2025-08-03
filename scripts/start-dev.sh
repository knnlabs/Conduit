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
  --fast         FAST startup - skip dependency checks (use when deps unchanged)
  --rebuild      Force reinstall all dependencies (use after package.json changes)
  --fix          Fix permissions and restart (use if you get EACCES errors)
  --build        Force rebuild containers
  --clean        Remove ALL containers/volumes and start fresh
  --clean-only   Clean without starting
  --logs         Show logs after startup
  --restart-webui Quick fix for stuck WebUI container (kills npm processes, restarts)
  --webui-logs   Show WebUI container logs only
  --with-minio   Enable MinIO S3-compatible storage for testing
  --help         Show this help message

Common Usage:
  $0               # Regular startup with cached volumes (RECOMMENDED)
  $0 --fast        # Daily use - starts in seconds, skips dependency checks
  $0 --rebuild     # After adding/removing packages
  $0 --build       # Force rebuild containers (slow)
  $0 --fix         # If you get permission errors
  $0 --clean       # Nuclear option: delete everything and start fresh
  $0 --restart-webui  # FIX STUCK WEBUI (when npm build breaks it)

After startup, services available at:
  - WebUI: http://localhost:3000
  - Core API Swagger: http://localhost:5000/swagger
  - Admin API Swagger: http://localhost:5002/swagger
  - RabbitMQ: http://localhost:15672
  - MinIO Console: http://localhost:9001 (minioadmin/minioadmin123, when --with-minio enabled)

Node modules are shared with host - you can:
  - Run 'npm run build' in ConduitLLM.WebUI/
  - Run 'npm run lint' directly
  - Use Claude Code without issues

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
    
    # Check if Docker is running with retry
    local docker_attempts=0
    local docker_max_attempts=3
    
    while [[ $docker_attempts -lt $docker_max_attempts ]]; do
        if docker info >/dev/null 2>&1; then
            break
        fi
        
        ((docker_attempts++))
        
        if [[ $docker_attempts -eq 1 ]]; then
            log_warn "Docker is not responding, attempting to recover..."
            
            # Try to start Docker service (works on Linux)
            if command -v systemctl >/dev/null 2>&1; then
                log_info "Attempting to start Docker service..."
                sudo systemctl start docker 2>/dev/null || true
                sleep 3
            elif command -v service >/dev/null 2>&1; then
                log_info "Attempting to start Docker service..."
                sudo service docker start 2>/dev/null || true
                sleep 3
            fi
        fi
        
        if [[ $docker_attempts -lt $docker_max_attempts ]]; then
            log_info "Waiting for Docker daemon... (attempt $docker_attempts/$docker_max_attempts)"
            sleep 2
        fi
    done
    
    if ! docker info >/dev/null 2>&1; then
        log_error "Docker is not running. Please start Docker manually."
        log_error "On Linux: sudo systemctl start docker"
        log_error "On Mac/Windows: Start Docker Desktop"
        exit 1
    fi
    
    # Check if Docker is responsive (not just running)
    if ! timeout 5 docker ps >/dev/null 2>&1; then
        log_warn "Docker daemon is slow to respond"
        log_info "AUTO-FIXING: Cleaning up Docker system..."
        
        # Try to clean up Docker to make it more responsive
        docker system prune -f --volumes 2>/dev/null || true
        
        # Give Docker a moment to recover
        sleep 2
        
        if ! timeout 5 docker ps >/dev/null 2>&1; then
            log_error "Docker daemon is not responding properly"
            log_error "Try restarting Docker manually"
            exit 1
        fi
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
    
    # Get ALL conduit containers
    local all_containers=$(docker ps -a --filter "name=conduit" --format "{{.Names}}\t{{.Image}}" || true)
    
    if [[ -n "$all_containers" ]]; then
        # Check for ANY non-development containers (production, mixed state, etc)
        local conflicting_containers=$(echo "$all_containers" | grep -v "node:\|alpine:\|postgres:\|rabbitmq:" || true)
        
        if [[ -n "$conflicting_containers" ]]; then
            log_warn "Found conflicting containers that need cleanup:"
            echo "$conflicting_containers"
            echo
            log_info "AUTO-FIXING: Cleaning up conflicting containers..."
            
            # Force stop ALL conduit containers
            docker ps -q --filter "name=conduit" | xargs -r docker kill 2>/dev/null || true
            
            # Remove ALL conduit containers
            docker ps -aq --filter "name=conduit" | xargs -r docker rm -f 2>/dev/null || true
            
            # Clean up with docker compose too
            local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
            timeout 5 $compose_cmd down --volumes --remove-orphans 2>/dev/null || true
            timeout 5 $compose_cmd -f docker-compose.yml down --volumes --remove-orphans 2>/dev/null || true
            
            # Remove any remaining volumes
            docker volume ls --filter "name=conduit" --format "{{.Name}}" | xargs -r docker volume rm -f 2>/dev/null || true
            
            log_info "Conflicting containers cleaned up successfully!"
            return 0
        fi
        
        # Also check for containers in bad states
        local bad_state_containers=$(docker ps -a --filter "name=conduit" --filter "status=exited" --filter "status=dead" --filter "status=restarting" --format "{{.Names}}" || true)
        if [[ -n "$bad_state_containers" ]]; then
            log_info "Cleaning up containers in bad states: $bad_state_containers"
            echo "$bad_state_containers" | xargs -r docker rm -f 2>/dev/null || true
        fi
    fi
    
    # Check for port conflicts (skip if tools not available)
    if command -v lsof >/dev/null 2>&1 || command -v netstat >/dev/null 2>&1 || command -v ss >/dev/null 2>&1; then
        local ports_to_check=("3000" "5000" "5002" "5432" "5672" "15672")
        # Add MinIO ports if enabled
        if [[ "${ENABLE_MINIO:-false}" == "true" ]]; then
            ports_to_check+=("9000" "9001")
        fi
        local port_conflicts=false
        
        for port in "${ports_to_check[@]}"; do
            local port_in_use=false
            
            # Try different methods to check port
            if command -v lsof >/dev/null 2>&1 && lsof -i ":$port" >/dev/null 2>&1; then
                port_in_use=true
            elif command -v ss >/dev/null 2>&1 && ss -ln | grep -q ":$port "; then
                port_in_use=true
            elif command -v netstat >/dev/null 2>&1 && netstat -an 2>/dev/null | grep -q ":$port.*LISTEN"; then
                port_in_use=true
            fi
            
            if [[ "$port_in_use" == "true" ]]; then
                # Check if it's our containers using the port
                local port_user=$(docker ps --filter "publish=$port" --format "{{.Names}}" | grep -i conduit || true)
                if [[ -z "$port_user" ]]; then
                    log_warn "Port $port is already in use by another process"
                    port_conflicts=true
                fi
            fi
        done
        
        if [[ "$port_conflicts" == "true" ]]; then
            log_warn "Some required ports are in use by other processes"
            log_info "AUTO-FIXING: Attempting to identify and handle port conflicts..."
            
            # Try to identify what's using the ports
            for port in "${ports_to_check[@]}"; do
                local process_info=""
                if command -v lsof >/dev/null 2>&1; then
                    process_info=$(lsof -i ":$port" 2>/dev/null | grep LISTEN | head -1 || true)
                fi
                
                if [[ -n "$process_info" ]]; then
                    log_warn "Port $port is used by: $process_info"
                fi
            done
            
            log_error "Cannot automatically fix port conflicts"
            log_error "You can either:"
            log_error "  1. Stop the conflicting processes"
            log_error "  2. Change the ports in docker-compose.yml"
            exit 1
        fi
    else
        log_info "Port checking tools not available, skipping port conflict check"
    fi
    
    log_info "Container conflict check completed"
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

# Check and fix permissions only if needed
check_and_fix_permissions_if_needed() {
    log_task "Checking if permission fixes are needed..."
    
    local current_uid=$(id -u)
    local current_gid=$(id -g)
    local needs_fix=false
    
    # Check host permissions
    local dirs_to_check=(
        "./ConduitLLM.WebUI"
        "./SDKs/Node/Admin"
        "./SDKs/Node/Core"
        "./SDKs/Node/Common"
    )
    
    for dir in "${dirs_to_check[@]}"; do
        if [[ -d "$dir" ]]; then
            local dir_owner=$(stat -c "%u" "$dir" 2>/dev/null || echo "unknown")
            if [[ "$dir_owner" != "$current_uid" ]]; then
                log_info "Directory $dir needs permission fix (owner: $dir_owner, expected: $current_uid)"
                needs_fix=true
                break
            fi
        fi
    done
    
    # Check if we can create a test file
    local test_file="./ConduitLLM.WebUI/.perm-test-$$"
    if ! touch "$test_file" 2>/dev/null; then
        log_info "Cannot create files in WebUI directory - permission fix needed"
        needs_fix=true
    else
        rm -f "$test_file" 2>/dev/null || true
    fi
    
    if [[ "$needs_fix" == "true" ]]; then
        log_info "Permission issues detected, applying fixes..."
        
        # Fix host permissions
        for dir in "${dirs_to_check[@]}"; do
            if [[ -d "$dir" ]]; then
                sudo chown -R "$current_uid:$current_gid" "$dir" 2>/dev/null || true
            fi
        done
        
        log_info "Host permissions fixed"
    else
        log_info "Permissions look good, no fixes needed"
    fi
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
    
    # FIRST: Kill everything with extreme prejudice
    log_info "KILLING all Docker containers with 'conduit' in the name..."
    docker ps -a | grep conduit | awk '{print $1}' | xargs -r docker rm -f 2>/dev/null || true
    
    # Remove volumes directly
    log_info "Removing all Conduit volumes..."
    docker volume ls | grep conduit | awk '{print $2}' | xargs -r docker volume rm -f 2>/dev/null || true
    
    # Now try compose cleanup with a timeout
    log_info "Running compose cleanup (5 second timeout)..."
    cd "$PROJECT_ROOT" || exit 1
    timeout 5 $compose_cmd down --volumes --remove-orphans 2>/dev/null || true
    
    # Kill all conduit containers immediately (in case compose missed any)
    log_info "Killing any remaining Conduit containers..."
    docker ps -q --filter "name=conduit" | xargs -r docker kill 2>/dev/null || true
    
    # Remove all conduit containers (in case compose missed any)
    log_info "Removing any remaining Conduit containers..."
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
    local max_retries=3
    local retry_count=0
    
    if [[ "${FORCE_BUILD:-false}" == "true" ]]; then
        # When --build is used, FORCE stop and remove existing containers first
        log_info "--build flag detected: Force stopping existing containers..."
        docker ps -q --filter "name=conduit" | xargs -r docker kill 2>/dev/null || true
        docker ps -aq --filter "name=conduit" | xargs -r docker rm -f 2>/dev/null || true
        build_flag="--build"
        log_info "Force building containers..."
    fi
    
    # Only fix permissions if needed (unless forced by --build or --clean flags)
    if [[ "${FORCE_BUILD:-false}" == "true" ]] || [[ "${FORCE_PERMISSION_FIX:-false}" == "true" ]]; then
        log_info "Forced permission fix requested"
        fix_host_permissions
        fix_volume_permissions
    else
        check_and_fix_permissions_if_needed
    fi
    
    # Export environment variables for docker-compose
    export SKIP_NPM_INSTALL="${SKIP_NPM_INSTALL:-false}"
    export FORCE_NPM_INSTALL="${FORCE_NPM_INSTALL:-false}"
    
    # Try to start services with retry logic
    while [[ $retry_count -lt $max_retries ]]; do
        log_info "Starting services (attempt $((retry_count + 1))/$max_retries)..."
        
        if $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml up -d $build_flag 2>&1; then
            # Check if containers actually started
            sleep 2
            local running_count=$(docker ps --filter "name=conduit" --filter "status=running" -q | wc -l)
            if [[ $running_count -gt 0 ]]; then
                break
            else
                log_warn "Containers failed to start properly"
            fi
        else
            log_warn "Docker compose command failed"
        fi
        
        ((retry_count++))
        
        if [[ $retry_count -lt $max_retries ]]; then
            log_info "AUTO-FIXING: Cleaning up and retrying..."
            
            # Clean up failed containers
            docker ps -aq --filter "name=conduit" | xargs -r docker rm -f 2>/dev/null || true
            
            # Clean up stale networks
            docker network prune -f 2>/dev/null || true
            
            # If compose keeps failing, try removing volumes
            if [[ $retry_count -eq 2 ]]; then
                log_warn "Multiple failures detected, cleaning volumes..."
                docker volume ls --filter "name=conduit" --format "{{.Name}}" | xargs -r docker volume rm -f 2>/dev/null || true
            fi
            
            sleep 2
        fi
    done
    
    if [[ $retry_count -eq $max_retries ]]; then
        log_error "Failed to start environment after $max_retries attempts"
        log_error "Try running: ./scripts/start-dev.sh --clean"
        exit 1
    fi
    
    # Wait a moment for containers to fully initialize
    sleep 3
    
    # Auto-fix any containers that failed to start
    local failed_containers=$(docker ps -a --filter "name=conduit" --filter "status=exited" --format "{{.Names}}" || true)
    if [[ -n "$failed_containers" ]]; then
        log_warn "Some containers failed to start: $failed_containers"
        log_info "AUTO-FIXING: Attempting to restart failed containers..."
        
        for container in $failed_containers; do
            log_info "Restarting $container..."
            docker start $container 2>/dev/null || {
                log_warn "Failed to restart $container, removing and recreating..."
                docker rm -f $container 2>/dev/null || true
                # Let docker-compose recreate it
                $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml up -d --no-deps $(echo $container | sed 's/conduit-//g' | sed 's/-[0-9]*$//g') 2>/dev/null || true
            }
        done
    fi
    
    log_info "Development environment started successfully!"
    echo
    log_info "Services available at:"
    log_info "  🌐 WebUI:               http://localhost:3000"
    log_info "  📚 Core API Swagger:    http://localhost:5000/swagger"
    log_info "  🔧 Admin API Swagger:   http://localhost:5002/swagger"  
    log_info "  🐰 RabbitMQ Management: http://localhost:15672 (conduit/conduitpass)"
    if [[ "$enable_minio" == "true" ]]; then
        log_info "  📦 MinIO Console:       http://localhost:9001 (minioadmin/minioadmin123)"
        log_info "  📦 MinIO API:           http://localhost:9000"
    fi
    echo
    log_info "You can now run commands directly on host:"
    log_info "  cd ConduitLLM.WebUI && npm run build      # Build WebUI"
    log_info "  cd ConduitLLM.WebUI && npm run lint       # Run linter"
    log_info "  cd SDKs/Node/Admin && npm run build       # Build SDK"
    echo
    log_info "For faster startup next time: ./scripts/start-dev.sh --fast"
    echo
    
    if [[ "$show_logs" == "true" ]]; then
        log_info "Showing container logs (press Ctrl+C to exit)..."
        $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml logs -f
    fi
}

# Quick restart WebUI container with cleanup
restart_webui() {
    log_task "Restarting WebUI container..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    
    # First, kill any stuck npm/node processes in the container
    log_info "Killing any stuck npm/node processes in WebUI container..."
    docker exec conduit-webui-1 sh -c "pkill -f 'npm run build' || true" 2>/dev/null || true
    docker exec conduit-webui-1 sh -c "pkill -f 'next build' || true" 2>/dev/null || true
    docker exec conduit-webui-1 sh -c "pkill -9 -f 'node.*next' || true" 2>/dev/null || true
    
    # Give processes a moment to die
    sleep 1
    
    # Stop the WebUI container gracefully
    log_info "Stopping WebUI container..."
    docker stop conduit-webui-1 2>/dev/null || true
    
    # Remove the container
    docker rm -f conduit-webui-1 2>/dev/null || true
    
    # Clear Next.js cache if it exists
    if [[ -d "./ConduitLLM.WebUI/.next" ]]; then
        log_info "Clearing Next.js build cache..."
        rm -rf "./ConduitLLM.WebUI/.next" 2>/dev/null || true
    fi
    
    # Restart just the WebUI service
    log_info "Starting WebUI container..."
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml up -d webui
    
    # Wait for it to be ready
    local max_attempts=15
    local attempt=1
    
    while [[ $attempt -le $max_attempts ]]; do
        if docker exec conduit-webui-1 echo "Container ready" >/dev/null 2>&1; then
            log_info "WebUI container is ready!"
            
            # Check if dev server is actually running
            sleep 3
            if docker exec conduit-webui-1 pgrep -f "next dev" >/dev/null 2>&1; then
                log_info "Next.js dev server is running"
                log_info "WebUI available at: http://localhost:3000"
                return 0
            else
                log_warn "Next.js dev server not detected, checking logs..."
                docker logs --tail 20 conduit-webui-1
            fi
            break
        fi
        
        log_info "Waiting for WebUI container... ($attempt/$max_attempts)"
        sleep 2
        ((attempt++))
    done
    
    if [[ $attempt -gt $max_attempts ]]; then
        log_error "WebUI container failed to start properly"
        log_info "Check logs with: docker logs conduit-webui-1"
        return 1
    fi
}

# Show WebUI logs
show_webui_logs() {
    log_task "Showing WebUI container logs..."
    
    if ! docker ps --format "{{.Names}}" | grep -q "conduit-webui-1"; then
        log_error "WebUI container is not running"
        log_info "Start it with: ./scripts/start-dev.sh"
        return 1
    fi
    
    log_info "Tailing WebUI logs (press Ctrl+C to exit)..."
    docker logs -f conduit-webui-1
}

# Check container health
check_health() {
    log_task "Checking container health..."
    
    local compose_cmd="${DOCKER_COMPOSE_CMD:-docker compose}"
    local max_attempts=30
    local attempt=1
    local expected_services=4
    
    while [[ $attempt -le $max_attempts ]]; do
        local healthy_count=$($compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" | wc -l)
        
        if [[ $healthy_count -ge $expected_services ]]; then
            log_info "All services are running"
            return 0
        fi
        
        # After 10 attempts, try auto-recovery
        if [[ $attempt -eq 10 ]]; then
            log_warn "Services slow to start, attempting auto-recovery..."
            
            # Find which services aren't running
            local all_services=$($compose_cmd -f docker-compose.yml -f docker-compose.dev.yml config --services)
            local running_services=$($compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running")
            
            for service in $all_services; do
                if ! echo "$running_services" | grep -q "^$service$"; then
                    log_info "AUTO-FIXING: Restarting $service..."
                    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml up -d --no-deps $service 2>/dev/null || true
                fi
            done
        fi
        
        # After 20 attempts, try more aggressive recovery
        if [[ $attempt -eq 20 ]]; then
            log_warn "Services still not healthy, trying aggressive recovery..."
            
            # Check for containers that keep restarting
            local restarting=$(docker ps --filter "name=conduit" --filter "status=restarting" --format "{{.Names}}" || true)
            if [[ -n "$restarting" ]]; then
                log_info "AUTO-FIXING: Removing restarting containers: $restarting"
                echo "$restarting" | xargs -r docker rm -f 2>/dev/null || true
                # Recreate them
                $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml up -d 2>/dev/null || true
            fi
        fi
        
        log_info "Waiting for services to start... ($attempt/$max_attempts)"
        sleep 2
        ((attempt++))
    done
    
    # Final recovery attempt
    log_warn "Health check failed after $max_attempts attempts"
    log_info "AUTO-FIXING: Final recovery attempt..."
    
    # Get detailed status
    local container_status=$($compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --format "table {{.Name}}\t{{.Status}}")
    log_info "Container status:"
    echo "$container_status"
    
    # Try one more restart of all services
    $compose_cmd -f docker-compose.yml -f docker-compose.dev.yml restart 2>/dev/null || true
    
    sleep 5
    
    # Final check
    local final_healthy_count=$($compose_cmd -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" | wc -l)
    if [[ $final_healthy_count -ge $expected_services ]]; then
        log_info "Services recovered successfully!"
        return 0
    else
        log_error "Failed to start all services"
        log_error "Check logs with: docker compose -f docker-compose.yml -f docker-compose.dev.yml logs"
        return 1
    fi
}

# Cleanup handler for script interruption
cleanup_on_exit() {
    local exit_code=$?
    if [[ $exit_code -ne 0 ]]; then
        log_warn "Script interrupted or failed"
        
        # If containers are in a bad state, offer to clean them
        local failed_containers=$(docker ps -a --filter "name=conduit" --filter "status=exited" --format "{{.Names}}" 2>/dev/null || true)
        if [[ -n "$failed_containers" ]]; then
            log_info "Found failed containers: $failed_containers"
            log_info "Run './scripts/start-dev.sh --clean' to start fresh"
        fi
    fi
}

# Main execution
main() {
    local force_build=false
    local clean=false
    local clean_only=false
    local fix_perms_only=false
    local show_logs=false
    local fast_mode=false
    local rebuild_mode=false
    local restart_webui_only=false
    local webui_logs_only=false
    local enable_minio=false
    
    # Set up cleanup trap
    trap cleanup_on_exit EXIT
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --fast)
                fast_mode=true
                shift
                ;;
            --rebuild)
                rebuild_mode=true
                shift
                ;;
            --fix)
                fix_perms_only=true
                shift
                ;;
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
            --fix-perms)  # Keep for backward compatibility
                fix_perms_only=true
                shift
                ;;
            --logs)
                show_logs=true
                shift
                ;;
            --restart-webui|--fix-webui)
                restart_webui_only=true
                shift
                ;;
            --webui-logs)
                webui_logs_only=true
                shift
                ;;
            --with-minio)
                enable_minio=true
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
    
    # Handle WebUI-specific operations first (these don't need full setup)
    if [[ "$restart_webui_only" == "true" ]]; then
        log_info "WebUI Quick Fix"
        log_info "==============="
        
        restart_webui
        return $?
    fi
    
    if [[ "$webui_logs_only" == "true" ]]; then
        show_webui_logs
        return $?
    fi
    
    # Export variables for docker-compose
    export FORCE_BUILD=$force_build
    export ENABLE_MINIO=$enable_minio
    
    # Set MinIO environment variables if enabled
    if [[ "$enable_minio" == "true" ]]; then
        export CONDUIT_MEDIA_STORAGE_TYPE=S3
        export CONDUIT_S3_ENDPOINT=http://minio:9000
        export CONDUIT_S3_ACCESS_KEY=minioadmin
        export CONDUIT_S3_SECRET_KEY=minioadmin123
        export CONDUIT_S3_BUCKET_NAME=conduit-media
        export CONDUIT_S3_REGION=us-east-1
        log_info "MinIO S3 storage enabled"
    fi
    
    # Set permission fix flag for --fix and --clean options
    if [[ "$fix_perms_only" == "true" ]] || [[ "$clean" == "true" ]]; then
        export FORCE_PERMISSION_FIX=true
    fi
    
    # Handle fast mode
    if [[ "$fast_mode" == "true" ]]; then
        export SKIP_NPM_INSTALL=true
        log_info "FAST MODE: Skipping dependency installation"
    fi
    
    # Handle rebuild mode
    if [[ "$rebuild_mode" == "true" ]]; then
        export FORCE_NPM_INSTALL=true
        log_info "REBUILD MODE: Force reinstalling all dependencies"
    fi
    
    if [[ "$fix_perms_only" == "true" ]]; then
        log_info "Fixing Permissions"
        log_info "=================="
        
        check_prerequisites
        
        # Fix host filesystem permissions
        fix_host_permissions
        
        # Stop and restart containers to fix any container issues
        log_info "Restarting containers to apply fixes..."
        docker compose -f docker-compose.yml -f docker-compose.dev.yml down
        
        setup_environment
        start_development "$show_logs"
        check_health
        
        log_info "Fix completed! Environment is ready."
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
    
    # If clean flag is set, clean FIRST before checking conflicts
    if [[ "$clean" == "true" ]]; then
        clean_environment
    else
        # Only check conflicts if not cleaning
        check_container_conflicts
    fi
    
    check_filesystem_compatibility
    validate_user_mapping
    validate_development_file_permissions
    
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