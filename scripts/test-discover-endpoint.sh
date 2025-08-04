#!/bin/bash

# Test the discover endpoint directly against the Admin API

PROVIDER_ID=${1:-1}
ADMIN_API_URL="http://localhost:5002"
API_KEY="your-admin-api-key"  # Replace with actual key if needed

echo "Testing discover endpoint for provider ID: $PROVIDER_ID"
echo "URL: $ADMIN_API_URL/api/ModelProviderMapping/discover/$PROVIDER_ID"

# Test the endpoint
curl -X GET \
  "$ADMIN_API_URL/api/ModelProviderMapping/discover/$PROVIDER_ID" \
  -H "Accept: application/json" \
  -H "X-API-Key: $API_KEY" \
  -v

echo ""
echo "If you see a 404, the endpoint might not exist in the Admin API."
echo "If you see a 401, you need to set the correct API key."
echo "If you see a 500, check the Admin API logs."