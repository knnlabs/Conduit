#!/bin/bash

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
CORE_API_PORT=5000
ADMIN_API_PORT=5002
MAX_WAIT_TIME=120  # seconds
RETRY_INTERVAL=5   # seconds

echo -e "${BLUE}🔨 Starting robust OpenAPI generation...${NC}"

# Function to log with timestamp
log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Function to check if a port is responding
check_port() {
    local port=$1
    local timeout=${2:-3}
    timeout $timeout bash -c "</dev/tcp/localhost/$port" 2>/dev/null
}

# Function to check if a service is ready and returning valid JSON
check_service_health() {
    local port=$1
    local endpoint="/swagger/v1/swagger.json"
    local url="http://localhost:$port$endpoint"
    
    if ! check_port $port 3; then
        return 1
    fi
    
    # Check if swagger endpoint returns valid JSON
    local response=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$url" 2>/dev/null || echo "000")
    if [[ "$response" == "200" ]]; then
        # Validate it's actually JSON
        if curl -s --max-time 5 "$url" | jq empty 2>/dev/null; then
            return 0
        fi
    fi
    return 1
}

# Function to wait for service to be ready
wait_for_service() {
    local service_name=$1
    local port=$2
    local max_wait=$3
    
    log "${YELLOW}⏳ Waiting for $service_name (port $port) to be ready...${NC}"
    
    local elapsed=0
    while [ $elapsed -lt $max_wait ]; do
        if check_service_health $port; then
            log "${GREEN}✅ $service_name is ready!${NC}"
            return 0
        fi
        
        sleep $RETRY_INTERVAL
        elapsed=$((elapsed + RETRY_INTERVAL))
        log "${YELLOW}   Still waiting for $service_name... (${elapsed}s/${max_wait}s)${NC}"
    done
    
    log "${RED}❌ $service_name failed to become ready within ${max_wait}s${NC}"
    return 1
}

# Function to ensure development services are running
ensure_services_running() {
    log "${BLUE}🔍 Checking if development services are running...${NC}"
    
    local core_ready=false
    local admin_ready=false
    
    # Quick check if services are already running
    if check_service_health $CORE_API_PORT; then
        log "${GREEN}✅ Core API already running on port $CORE_API_PORT${NC}"
        core_ready=true
    fi
    
    if check_service_health $ADMIN_API_PORT; then
        log "${GREEN}✅ Admin API already running on port $ADMIN_API_PORT${NC}"
        admin_ready=true
    fi
    
    # If both services are ready, we're good
    if $core_ready && $admin_ready; then
        return 0
    fi
    
    # Check if we have Docker
    if ! command -v docker &> /dev/null; then
        log "${RED}❌ Docker not found. Cannot start services automatically.${NC}"
        return 1
    fi
    
    # Start development environment
    log "${BLUE}🚀 Starting development environment...${NC}"
    cd "$PROJECT_ROOT"
    
    if [[ -x "./scripts/start-dev.sh" ]]; then
        log "${YELLOW}   Running ./scripts/start-dev.sh${NC}"
        ./scripts/start-dev.sh > /dev/null 2>&1 &
        local start_pid=$!
        
        # Wait for services to become ready
        local services_ready=true
        
        if ! $core_ready; then
            if ! wait_for_service "Core API" $CORE_API_PORT $MAX_WAIT_TIME; then
                services_ready=false
            fi
        fi
        
        if ! $admin_ready; then
            if ! wait_for_service "Admin API" $ADMIN_API_PORT $MAX_WAIT_TIME; then
                services_ready=false
            fi
        fi
        
        if ! $services_ready; then
            log "${RED}❌ Failed to start all required services${NC}"
            return 1
        fi
        
        log "${GREEN}✅ All services are ready!${NC}"
        return 0
    else
        log "${RED}❌ ./scripts/start-dev.sh not found or not executable${NC}"
        return 1
    fi
}

# Function to download OpenAPI spec from running service
download_openapi_spec() {
    local service_name=$1
    local port=$2
    local output_file=$3
    
    local url="http://localhost:$port/swagger/v1/swagger.json"
    
    log "${BLUE}📥 Downloading $service_name OpenAPI spec from $url${NC}"
    
    if curl -s --max-time 10 "$url" > "$output_file.tmp"; then
        # Validate the downloaded JSON
        if jq empty "$output_file.tmp" 2>/dev/null; then
            mv "$output_file.tmp" "$output_file"
            log "${GREEN}✅ Successfully downloaded $service_name OpenAPI spec${NC}"
            return 0
        else
            log "${RED}❌ Downloaded $service_name spec is not valid JSON${NC}"
            rm -f "$output_file.tmp"
            return 1
        fi
    else
        log "${RED}❌ Failed to download $service_name OpenAPI spec${NC}"
        rm -f "$output_file.tmp"
        return 1
    fi
}

# Function to generate OpenAPI specs using Swashbuckle CLI (fallback)
generate_openapi_cli() {
    log "${YELLOW}📄 Falling back to Swashbuckle CLI method...${NC}"
    
    cd "$PROJECT_ROOT"
    
    # Build the projects
    log "${BLUE}🔨 Building .NET projects...${NC}"
    if ! dotnet build ConduitLLM.Http/ConduitLLM.Http.csproj --verbosity quiet; then
        log "${RED}❌ Failed to build Core API project${NC}"
        return 1
    fi
    
    if ! dotnet build ConduitLLM.Admin/ConduitLLM.Admin.csproj --verbosity quiet; then
        log "${RED}❌ Failed to build Admin API project${NC}"
        return 1
    fi
    
    # Ensure Swashbuckle CLI is installed
    log "${BLUE}🔧 Ensuring Swashbuckle CLI is available...${NC}"
    dotnet tool install -g Swashbuckle.AspNetCore.Cli --version 8.1.1 > /dev/null 2>&1 || true
    
    # Generate Admin API spec (Core API spec should exist)
    local swagger_tool="$HOME/.dotnet/tools/swagger"
    if [[ ! -x "$swagger_tool" ]]; then
        swagger_tool="swagger"  # Try global path
    fi
    
    log "${BLUE}📄 Generating Admin API OpenAPI spec...${NC}"
    if $swagger_tool tofile --output ConduitLLM.Admin/openapi-admin.json ConduitLLM.Admin/bin/Debug/net9.0/ConduitLLM.Admin.dll v1 2>/dev/null; then
        log "${GREEN}✅ Generated Admin API spec using CLI${NC}"
    else
        log "${YELLOW}⚠️  CLI generation failed, but continuing...${NC}"
    fi
}

# Function to check if existing OpenAPI files are usable
check_existing_files() {
    local core_file="$PROJECT_ROOT/ConduitLLM.Http/openapi-core.json"
    local admin_file="$PROJECT_ROOT/ConduitLLM.Admin/openapi-admin.json"
    
    local core_valid=false
    local admin_valid=false
    
    if [[ -f "$core_file" ]] && jq empty "$core_file" 2>/dev/null; then
        log "${GREEN}✅ Found valid existing Core API spec${NC}"
        core_valid=true
    fi
    
    if [[ -f "$admin_file" ]] && jq empty "$admin_file" 2>/dev/null; then
        log "${GREEN}✅ Found valid existing Admin API spec${NC}"
        admin_valid=true
    fi
    
    if $core_valid && $admin_valid; then
        return 0
    else
        return 1
    fi
}

# Function to ensure npm dependencies are installed
ensure_npm_dependencies() {
    cd "$SCRIPT_DIR"
    
    if [[ ! -d "node_modules" ]] || [[ ! -f "node_modules/.package-lock.json" ]]; then
        log "${BLUE}📦 Installing npm dependencies...${NC}"
        if npm install --silent; then
            log "${GREEN}✅ npm dependencies installed${NC}"
        else
            log "${RED}❌ Failed to install npm dependencies${NC}"
            return 1
        fi
    fi
}

# Function to generate TypeScript types
generate_typescript_types() {
    log "${BLUE}🔤 Generating TypeScript types...${NC}"
    
    cd "$SCRIPT_DIR"
    
    # Ensure dependencies are installed
    if ! ensure_npm_dependencies; then
        return 1
    fi
    
    # Generate types from files
    if npm run generate:from-files > /dev/null 2>&1; then
        log "${GREEN}✅ TypeScript types generated successfully${NC}"
        
        # Format the generated files
        if npm run format > /dev/null 2>&1; then
            log "${GREEN}✅ Generated files formatted${NC}"
        else
            log "${YELLOW}⚠️  Failed to format generated files (continuing)${NC}"
        fi
        
        return 0
    else
        log "${RED}❌ Failed to generate TypeScript types${NC}"
        return 1
    fi
}

# Main execution
main() {
    local generation_successful=false
    
    # Strategy 1: Try to use running Docker services
    if ensure_services_running; then
        log "${BLUE}🎯 Strategy 1: Using running Docker services${NC}"
        
        local core_success=false
        local admin_success=false
        
        # Download Core API spec
        if download_openapi_spec "Core API" $CORE_API_PORT "$PROJECT_ROOT/ConduitLLM.Http/openapi-core.json"; then
            core_success=true
        fi
        
        # Download Admin API spec
        if download_openapi_spec "Admin API" $ADMIN_API_PORT "$PROJECT_ROOT/ConduitLLM.Admin/openapi-admin.json"; then
            admin_success=true
        fi
        
        if $core_success && $admin_success; then
            generation_successful=true
        elif $core_success || $admin_success; then
            log "${YELLOW}⚠️  Partial success with Docker strategy${NC}"
            generation_successful=true
        fi
    fi
    
    # Strategy 2: Fallback to CLI generation
    if ! $generation_successful; then
        log "${BLUE}🎯 Strategy 2: Using Swashbuckle CLI${NC}"
        if generate_openapi_cli; then
            generation_successful=true
        fi
    fi
    
    # Strategy 3: Check for existing files
    if ! $generation_successful; then
        log "${BLUE}🎯 Strategy 3: Using existing OpenAPI files${NC}"
        if check_existing_files; then
            log "${YELLOW}⚠️  Using existing OpenAPI files (may be outdated)${NC}"
            generation_successful=true
        fi
    fi
    
    # Final check
    if ! $generation_successful; then
        log "${RED}❌ All OpenAPI generation strategies failed${NC}"
        exit 1
    fi
    
    # Generate TypeScript types
    log "${BLUE}🔤 Generating TypeScript SDK types...${NC}"
    if generate_typescript_types; then
        log "${GREEN}🎉 OpenAPI generation and TypeScript generation completed successfully!${NC}"
        
        # Show summary
        echo
        log "${GREEN}📋 Summary:${NC}"
        log "${GREEN}   - Core API: ConduitLLM.Http/openapi-core.json${NC}"
        log "${GREEN}   - Admin API: ConduitLLM.Admin/openapi-admin.json${NC}"
        log "${GREEN}   - Core SDK: SDKs/Node/Core/src/generated/core-api.ts${NC}"
        log "${GREEN}   - Admin SDK: SDKs/Node/Admin/src/generated/admin-api.ts${NC}"
        
        exit 0
    else
        log "${RED}❌ OpenAPI generation succeeded but TypeScript generation failed${NC}"
        exit 1
    fi
}

# Check prerequisites
if ! command -v jq &> /dev/null; then
    log "${RED}❌ jq is required but not installed. Please install jq first.${NC}"
    exit 1
fi

if ! command -v curl &> /dev/null; then
    log "${RED}❌ curl is required but not installed. Please install curl first.${NC}"
    exit 1
fi

# Run main function
main "$@"