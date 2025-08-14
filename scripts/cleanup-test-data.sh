#\!/bin/bash

# Cleanup Test Data Script for Conduit Integration Tests
# This script removes all test data from the database before running tests

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color
BOLD='\033[1m'

echo -e "${BLUE}${BOLD}🧹 Cleaning up test data...${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Get the script directory and navigate to root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"

# Load .env file if it exists
if [ -f "$ROOT_DIR/.env" ]; then
    export $(cat "$ROOT_DIR/.env" | grep -v '^#' | xargs)
fi

# Database connection details
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-conduit}"
DB_USER="${DB_USER:-conduit}"
DB_PASSWORD="${DB_PASSWORD:-conduitpass}"

# Connect to PostgreSQL and clean test data
export PGPASSWORD="$DB_PASSWORD"

echo -e "${YELLOW}Removing test data from database...${NC}"

# SQL command to delete test data
SQL_CLEANUP='
-- Delete test virtual keys
DELETE FROM "VirtualKeys" WHERE "VirtualKey" LIKE '\''condt_%'\'';

-- Delete test virtual key groups
DELETE FROM "VirtualKeyGroups" WHERE "Name" LIKE '\''TEST_%'\'';

-- Delete test model costs
DELETE FROM "ModelCosts" WHERE "Name" LIKE '\''TEST_%'\'';

-- Delete test model mappings
DELETE FROM "ModelProviderMappings" WHERE "ModelId" LIKE '\''TEST_%'\'';

-- Delete test provider keys
DELETE FROM "ProviderKeyCredentials" WHERE "KeyName" LIKE '\''TEST_%'\'';

-- Delete test providers
DELETE FROM "Providers" WHERE "ProviderName" LIKE '\''TEST_%'\'';
'

# Execute cleanup - don't fail if database is not accessible
psql -h "$DB_HOST" -p "$DB_PORT" -d "$DB_NAME" -U "$DB_USER" -c "$SQL_CLEANUP" 2>/dev/null || true

echo -e "${GREEN}✓ Test data cleaned${NC}"

# Clean test reports
REPORT_DIR="$ROOT_DIR/ConduitLLM.IntegrationTests/bin/Debug/net9.0/Reports"
if [ -d "$REPORT_DIR" ]; then
    echo -e "${YELLOW}Removing old test reports...${NC}"
    rm -f "$REPORT_DIR"/*.md 2>/dev/null || true
    echo -e "${GREEN}✓ Test reports cleaned${NC}"
fi

# Clean test context
CONTEXT_FILE="$ROOT_DIR/ConduitLLM.IntegrationTests/bin/Debug/net9.0/test-context.json"
if [ -f "$CONTEXT_FILE" ]; then
    rm -f "$CONTEXT_FILE"
    echo -e "${GREEN}✓ Test context cleaned${NC}"
fi

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}${BOLD}✅ Cleanup complete\!${NC}"
echo

# Exit successfully
exit 0
