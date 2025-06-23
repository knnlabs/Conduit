#!/bin/bash

echo "Testing video generation debugging..."

# Test 1: Check if model is in the available models list
echo -e "\n1. Checking available models:"
curl -s http://localhost:5000/v1/models \
  -H "Authorization: Bearer condt_wyfGh4rrLUIRhxHYqYbFpLZkC/nqfoT00e6YeOmMLXQ=" | jq '.data[] | select(.id == "video-01")'

# Test 2: Check model capability via discovery endpoint
echo -e "\n2. Checking video capability via discovery:"
curl -s http://localhost:5000/v1/discovery/models/video-01/capabilities/VideoGeneration \
  -H "Authorization: Bearer condt_wyfGh4rrLUIRhxHYqYbFpLZkC/nqfoT00e6YeOmMLXQ=" | jq .

# Test 3: Try video generation with detailed error
echo -e "\n3. Attempting video generation:"
curl -X POST http://localhost:5000/v1/videos/generations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_wyfGh4rrLUIRhxHYqYbFpLZkC/nqfoT00e6YeOmMLXQ=" \
  -d '{
    "model": "video-01", 
    "prompt": "A cat playing with a ball",
    "duration": 5
  }' \
  -w "\nHTTP Status: %{http_code}\n" \
  -s | jq .