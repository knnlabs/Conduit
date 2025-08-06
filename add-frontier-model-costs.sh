#!/bin/bash

# Add Frontier Model Costs Script
# This script adds default model costs for Anthropic and OpenAI models
# using the updated ModelCost + ModelCostMapping architecture

set -e  # Exit on any error

# Configuration
DB_CONTAINER="conduit-postgres-1"  # Adjust if your container name is different
DB_USER="conduit"
DB_NAME="conduitdb"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_SCRIPT="$SCRIPT_DIR/add-frontier-model-costs-updated.sql"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if Docker is running
check_docker() {
    if ! docker info >/dev/null 2>&1; then
        print_error "Docker is not running. Please start Docker and try again."
        exit 1
    fi
}

# Function to check if database container exists and is running
check_database() {
    if ! docker ps --format "table {{.Names}}" | grep -q "$DB_CONTAINER"; then
        print_error "Database container '$DB_CONTAINER' is not running."
        print_error "Please start your Conduit services with: docker compose up -d"
        exit 1
    fi
}

# Function to test database connection
test_connection() {
    if ! docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "SELECT 1;" >/dev/null 2>&1; then
        print_error "Cannot connect to database. Please check your database configuration."
        exit 1
    fi
}

# Function to execute SQL script
execute_sql() {
    print_status "Executing model cost configuration script..."
    
    if docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" < "$SQL_SCRIPT"; then
        print_success "Model costs have been successfully configured!"
    else
        print_error "Failed to execute SQL script. Please check the error messages above."
        exit 1
    fi
}

# Function to show usage information
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -h, --help     Show this help message"
    echo "  -c, --container CONTAINER_NAME"
    echo "                 Specify database container name (default: $DB_CONTAINER)"
    echo "  -u, --user USER_NAME"
    echo "                 Specify database user (default: $DB_USER)"
    echo "  -d, --database DB_NAME"
    echo "                 Specify database name (default: $DB_NAME)"
    echo ""
    echo "This script adds default model costs for Anthropic and OpenAI models"
    echo "to your Conduit database using the modern ModelCost architecture."
    echo ""
    echo "Examples:"
    echo "  $0                                    # Use default settings"
    echo "  $0 -c my-postgres-container          # Use custom container name"
    echo "  $0 -u postgres -d conduit_production # Use custom user and database"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_usage
            exit 0
            ;;
        -c|--container)
            DB_CONTAINER="$2"
            shift 2
            ;;
        -u|--user)
            DB_USER="$2"
            shift 2
            ;;
        -d|--database)
            DB_NAME="$2"
            shift 2
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Main execution
main() {
    print_status "Starting Conduit Model Cost Configuration"
    print_status "Database Container: $DB_CONTAINER"
    print_status "Database User: $DB_USER"
    print_status "Database Name: $DB_NAME"
    echo ""
    
    # Check if SQL script exists
    if [[ ! -f "$SQL_SCRIPT" ]]; then
        print_error "SQL script not found: $SQL_SCRIPT"
        print_error "Please make sure the script is in the same directory as this shell script."
        exit 1
    fi
    
    # Perform checks
    print_status "Checking Docker..."
    check_docker
    
    print_status "Checking database container..."
    check_database
    
    print_status "Testing database connection..."
    test_connection
    
    # Execute the SQL script
    execute_sql
    
    echo ""
    print_success "Model cost configuration completed successfully!"
    print_warning "Note: The costs have been created but not yet linked to specific models."
    print_warning "To link costs to models, either:"
    print_warning "1. Use the Admin UI at http://localhost:5000/model-costs"
    print_warning "2. Use the Admin API to create ModelCostMappings"
    print_warning "3. Run additional SQL commands as shown in the script comments"
    echo ""
    print_status "You can view the created costs by running:"
    print_status "docker exec $DB_CONTAINER psql -U $DB_USER -d $DB_NAME -c \"SELECT * FROM \\\"ModelCosts\\\" WHERE \\\"CostName\\\" LIKE '%Anthropic%' OR \\\"CostName\\\" LIKE '%OpenAI%';\""
}

# Run main function
main "$@"
