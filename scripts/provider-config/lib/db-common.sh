#!/bin/bash
# Common database connection utilities for provider configuration scripts

# Database connection parameters
DB_HOST="${DB_HOST:-postgres}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-conduitdb}"
DB_USER="${DB_USER:-conduit}"
DB_PASSWORD="${DB_PASSWORD:-conduitpass}"

# Container name (can be overridden)
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-conduit-postgres-1}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if running in Docker or directly
check_environment() {
    if [ -f /.dockerenv ] || [ -n "$DOCKER_CONTAINER" ]; then
        echo "Running inside Docker container"
        USE_DOCKER=false
    else
        echo "Running on host - will use Docker exec"
        USE_DOCKER=true
    fi
}

# Execute PostgreSQL command
# Usage: exec_psql "SQL command"
exec_psql() {
    local sql="$1"
    local db="${2:-$DB_NAME}"
    
    if [ "$USE_DOCKER" = true ]; then
        docker exec -i "$POSTGRES_CONTAINER" psql -U "$DB_USER" -d "$db" -t -c "$sql" 2>/dev/null
    else
        PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$db" -t -c "$sql" 2>/dev/null
    fi
}

# Execute PostgreSQL command with full output (including column headers)
# Usage: exec_psql_full "SQL command"
exec_psql_full() {
    local sql="$1"
    local db="${2:-$DB_NAME}"
    
    if [ "$USE_DOCKER" = true ]; then
        docker exec -i "$POSTGRES_CONTAINER" psql -U "$DB_USER" -d "$db" -c "$sql" 2>/dev/null
    else
        PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$db" -c "$sql" 2>/dev/null
    fi
}

# Execute PostgreSQL command and return JSON
# Usage: exec_psql_json "SQL command"
exec_psql_json() {
    local sql="$1"
    local db="${2:-$DB_NAME}"
    
    if [ "$USE_DOCKER" = true ]; then
        docker exec -i "$POSTGRES_CONTAINER" psql -U "$DB_USER" -d "$db" -t -A -c "$sql" 2>/dev/null
    else
        PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$db" -t -A -c "$sql" 2>/dev/null
    fi
}

# Import SQL from stdin
# Usage: echo "SQL" | import_sql
import_sql() {
    local db="${1:-$DB_NAME}"
    
    if [ "$USE_DOCKER" = true ]; then
        docker exec -i "$POSTGRES_CONTAINER" psql -U "$DB_USER" -d "$db" 2>/dev/null
    else
        PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$db" 2>/dev/null
    fi
}

# Check database connection
check_db_connection() {
    echo -n "Checking database connection... "
    
    local result=$(exec_psql "SELECT 1" 2>&1)
    if [ "$?" -eq 0 ]; then
        echo -e "${GREEN}✓ Connected${NC}"
        return 0
    else
        echo -e "${RED}✗ Failed${NC}"
        echo "Error: Unable to connect to database"
        echo "Please check your database settings and ensure the database is running"
        return 1
    fi
}

# Check if tables exist
check_tables_exist() {
    local tables=("Providers" "ProviderKeyCredentials" "ModelProviderMappings")
    local missing_tables=()
    
    for table in "${tables[@]}"; do
        local exists=$(exec_psql "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$table');" | tr -d ' \r\n')
        if [ "$exists" != "t" ]; then
            missing_tables+=("$table")
        fi
    done
    
    if [ ${#missing_tables[@]} -gt 0 ]; then
        echo -e "${YELLOW}Warning: Missing tables: ${missing_tables[*]}${NC}"
        return 1
    fi
    
    return 0
}

# Get count of records in a table
get_table_count() {
    local table="$1"
    local count=$(exec_psql "SELECT COUNT(*) FROM \"$table\";" | tr -d ' \r\n')
    echo "$count"
}

# Create backup of tables before import
create_backup() {
    local backup_file="$1"
    echo "Creating backup of provider tables..."
    
    local tables=("Providers" "ProviderKeyCredentials" "ModelProviderMappings")
    
    if [ "$USE_DOCKER" = true ]; then
        docker exec "$POSTGRES_CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" \
            $(printf -- '-t "%s" ' "${tables[@]}") \
            --data-only --inserts > "$backup_file" 2>/dev/null
    else
        PGPASSWORD="$DB_PASSWORD" pg_dump -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" \
            $(printf -- '-t "%s" ' "${tables[@]}") \
            --data-only --inserts > "$backup_file" 2>/dev/null
    fi
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Backup created: $backup_file${NC}"
        return 0
    else
        echo -e "${RED}✗ Backup failed${NC}"
        return 1
    fi
}

# Initialize environment check
check_environment