#!/bin/bash

echo "Testing SignalR negotiate endpoints..."
echo ""

# Test Core API notifications hub
echo "1. Testing Core API notifications hub negotiate (with Bearer token):"
curl -X POST http://localhost:5000/hubs/notifications/negotiate?negotiateVersion=1 \
  -H "Authorization: Bearer test-key" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:5001" \
  -v

echo ""
echo "2. Testing Core API notifications hub negotiate (with access_token query param):"
curl -X POST "http://localhost:5000/hubs/notifications/negotiate?negotiateVersion=1&access_token=test-key" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:5001" \
  -v

echo ""
echo "3. Testing Admin API admin-notifications hub negotiate (with X-API-Key header):"
curl -X POST http://localhost:5002/hubs/admin-notifications/negotiate?negotiateVersion=1 \
  -H "X-API-Key: alpha" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:5001" \
  -v

echo ""
echo "4. Testing Admin API admin-notifications hub negotiate (with access_token query param):"
curl -X POST "http://localhost:5002/hubs/admin-notifications/negotiate?negotiateVersion=1&access_token=alpha" \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:5001" \
  -v