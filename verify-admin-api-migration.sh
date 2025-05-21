#!/bin/bash
# Script to verify Admin API migration
# This script checks if the Admin API is properly configured and accessible

set -e

# Default variables
WEBUI_URL="http://localhost:5001"
ADMIN_API_URL="http://localhost:5002"
MASTER_KEY="alpha"  # Default master key, should be changed in production

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --webui-url) WEBUI_URL="$2"; shift ;;
        --admin-url) ADMIN_API_URL="$2"; shift ;;
        --key) MASTER_KEY="$2"; shift ;;
        --help) 
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --webui-url URL    WebUI URL (default: http://localhost:5001)"
            echo "  --admin-url URL    Admin API URL (default: http://localhost:5002)"
            echo "  --key KEY          Master key (default: alpha)"
            echo "  --help             Show this help message"
            exit 0
            ;;
        *) echo "Unknown parameter: $1"; exit 1 ;;
    esac
    shift
done

echo -e "${YELLOW}=== Conduit Admin API Migration Verification ===${NC}"
echo "Testing WebUI URL: $WEBUI_URL"
echo "Testing Admin API URL: $ADMIN_API_URL"
echo ""

# Step 1: Check if WebUI is accessible
echo -e "${YELLOW}1. Checking WebUI accessibility...${NC}"
if curl -s -f "$WEBUI_URL" > /dev/null; then
    echo -e "${GREEN}✓ WebUI is accessible${NC}"
else
    echo -e "${RED}✗ WebUI is not accessible at $WEBUI_URL${NC}"
    echo "  Please check if the WebUI service is running"
    exit 1
fi

# Step 2: Check if Admin API is accessible
echo -e "${YELLOW}2. Checking Admin API accessibility...${NC}"
if curl -s -f "$ADMIN_API_URL/health" > /dev/null; then
    echo -e "${GREEN}✓ Admin API is accessible${NC}"
else
    echo -e "${RED}✗ Admin API is not accessible at $ADMIN_API_URL${NC}"
    echo "  Please check if the Admin API service is running"
    exit 1
fi

# Step 3: Check authentication with Admin API
echo -e "${YELLOW}3. Testing Admin API authentication...${NC}"
AUTH_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $MASTER_KEY" "$ADMIN_API_URL/api/systeminfo")

if [ "$AUTH_RESPONSE" == "200" ]; then
    echo -e "${GREEN}✓ Admin API authentication successful${NC}"
else
    echo -e "${RED}✗ Admin API authentication failed (HTTP $AUTH_RESPONSE)${NC}"
    echo "  Please check if the master key is correctly configured"
    echo "  WebUI CONDUIT_MASTER_KEY should match Admin API's AdminApi__MasterKey"
    exit 1
fi

# Step 4: Test a basic API call
echo -e "${YELLOW}4. Testing basic API functionality...${NC}"
VIRTUAL_KEYS_RESPONSE=$(curl -s -H "Authorization: Bearer $MASTER_KEY" "$ADMIN_API_URL/api/virtualkeys")

if [[ $VIRTUAL_KEYS_RESPONSE == \[*\] ]]; then
    echo -e "${GREEN}✓ Admin API returned virtual keys list successfully${NC}"
else
    echo -e "${RED}✗ Admin API failed to return virtual keys${NC}"
    echo "  API response: $VIRTUAL_KEYS_RESPONSE"
    echo "  Please check Admin API logs for errors"
    exit 1
fi

# Step 5: Check WebUI environment variables
echo -e "${YELLOW}5. Checking WebUI configuration...${NC}"
echo "  To verify WebUI is properly configured, check these environment variables:"
echo "  - CONDUIT_ADMIN_API_BASE_URL should be: $ADMIN_API_URL"
echo "  - CONDUIT_USE_ADMIN_API should be: true"
echo "  - CONDUIT_DISABLE_DIRECT_DB_ACCESS should be: true"
echo "  - CONDUIT_MASTER_KEY should match AdminApi__MasterKey in Admin API"
echo ""
echo -e "${YELLOW}Manual verification:${NC}"
echo "  1. Log in to WebUI at $WEBUI_URL"
echo "  2. Check for any deprecation warnings (there should be none if migration is complete)"
echo "  3. Test key functionality: create/view/edit virtual keys"
echo "  4. Check logs for any errors related to Admin API communication"

echo ""
echo -e "${GREEN}=== Migration Verification Completed Successfully ===${NC}"
echo "Your Conduit instance appears to be correctly configured for Admin API mode."
echo "For more details, see the migration guide: docs/admin-api-migration-guide.md"