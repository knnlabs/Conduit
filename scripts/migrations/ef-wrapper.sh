#!/bin/bash
set -e

# Script: ef-wrapper.sh
# Purpose: Wrapper for EF Core commands with enhanced error handling and debugging
# This ensures consistent environment setup and provides better error messages

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    local status=$1
    local message=$2
    case $status in
        "error")
            echo -e "${RED}✗ ERROR:${NC} $message" >&2
            ;;
        "success")
            echo -e "${GREEN}✓${NC} $message"
            ;;
        "warning")
            echo -e "${YELLOW}⚠${NC} $message"
            ;;
        "info")
            echo -e "${BLUE}ℹ${NC} $message"
            ;;
    esac
}

# Function to validate environment
validate_environment() {
    local has_error=false
    
    print_info "Validating environment..."
    
    # Check DATABASE_URL
    if [ -z "$DATABASE_URL" ]; then
        print_error "DATABASE_URL environment variable is not set"
        echo "  Set DATABASE_URL to a valid PostgreSQL connection string:"
        echo "  Example: postgresql://user:password@localhost:5432/conduitdb"
        has_error=true
    else
        print_success "DATABASE_URL is set"
        # Validate format (basic check)
        if [[ ! "$DATABASE_URL" =~ ^(postgresql|postgres):// ]] && [[ ! "$DATABASE_URL" =~ Host= ]]; then
            print_warning "DATABASE_URL format may be invalid"
            echo "  Expected format: postgresql://user:password@host:port/database"
            echo "  Or: Host=host;Port=port;Database=database;Username=user;Password=password"
        fi
    fi
    
    # Check if we're in the correct directory
    if [ ! -f "ConduitLLM.Configuration.csproj" ]; then
        print_error "Not in ConduitLLM.Configuration directory"
        echo "  Please run this script from the ConduitLLM.Configuration directory"
        has_error=true
    else
        print_success "In correct project directory"
    fi
    
    # Check if EF Core tools are installed
    if ! command -v dotnet-ef &> /dev/null; then
        print_error "EF Core tools not installed"
        echo "  Install with: dotnet tool install --global dotnet-ef"
        has_error=true
    else
        local ef_version=$(dotnet-ef --version 2>/dev/null || echo "unknown")
        print_success "EF Core tools installed (version: $ef_version)"
    fi
    
    # Check if project is built
    if [ ! -d "bin" ] || [ ! -d "obj" ]; then
        print_warning "Project may not be built"
        echo "  Run: dotnet build"
    fi
    
    if [ "$has_error" = true ]; then
        return 1
    fi
    
    return 0
}

# Function to test database connection
test_database_connection() {
    print_info "Testing database connection..."
    
    # Extract connection details from DATABASE_URL
    if [[ "$DATABASE_URL" =~ ^(postgresql|postgres)://([^:]+):([^@]+)@([^:]+):([0-9]+)/(.+)$ ]]; then
        local host="${BASH_REMATCH[4]}"
        local port="${BASH_REMATCH[5]}"
        
        # Test if PostgreSQL is reachable
        if timeout 5 bash -c "echo > /dev/tcp/$host/$port" 2>/dev/null; then
            print_success "PostgreSQL server is reachable at $host:$port"
        else
            print_error "Cannot connect to PostgreSQL at $host:$port"
            echo "  Ensure PostgreSQL is running and accessible"
            return 1
        fi
    fi
    
    return 0
}

# Function to run EF command with enhanced error handling
run_ef_command() {
    local command="$@"
    local temp_output=$(mktemp)
    local exit_code=0
    
    print_info "Running: dotnet ef $command"
    
    # Run the command and capture output
    if dotnet ef $command > "$temp_output" 2>&1; then
        cat "$temp_output"
        print_success "Command completed successfully"
    else
        exit_code=$?
        cat "$temp_output"
        
        # Analyze common error patterns
        if grep -q "Unable to create a 'DbContext'" "$temp_output"; then
            print_error "Failed to create DbContext"
            echo "  This usually means the database connection failed"
            echo "  Check your DATABASE_URL and ensure PostgreSQL is running"
        elif grep -q "No project was found" "$temp_output"; then
            print_error "No project found"
            echo "  Ensure you're in the correct directory with a .csproj file"
        elif grep -q "Build failed" "$temp_output"; then
            print_error "Build failed"
            echo "  Run: dotnet build"
        fi
        
        print_error "Command failed with exit code: $exit_code"
    fi
    
    rm -f "$temp_output"
    return $exit_code
}

# Main execution
main() {
    echo "=============================================="
    echo "EF Core Command Wrapper"
    echo "=============================================="
    
    # Validate environment first
    if ! validate_environment; then
        echo ""
        print_error "Environment validation failed"
        exit 1
    fi
    
    # Test database connection
    if ! test_database_connection; then
        echo ""
        print_warning "Database connection test failed"
        echo "  Continuing anyway - some commands may work without a live database"
    fi
    
    echo ""
    echo "Running EF Core command..."
    echo "------------------------------"
    
    # Run the actual command
    run_ef_command "$@"
    exit_code=$?
    
    echo "------------------------------"
    
    if [ $exit_code -eq 0 ]; then
        print_success "Operation completed successfully"
    else
        print_error "Operation failed"
    fi
    
    exit $exit_code
}

# Alias functions for print_* to match the actual function names
print_error() { print_status "error" "$1"; }
print_success() { print_status "success" "$1"; }
print_warning() { print_status "warning" "$1"; }
print_info() { print_status "info" "$1"; }

# Run main function with all arguments
main "$@"