#!/bin/bash

echo "Testing video generation with long wait time..."

# Start video generation
echo "Starting video generation..."
RESPONSE=$(curl -X POST http://localhost:5000/v1/videos/generations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_wyfGh4rrLUIRhxHYqYbFpLZkC/nqfoT00e6YeOmMLXQ=" \
  -d '{
    "model": "video-01", 
    "prompt": "A simple animated logo",
    "duration": 3,
    "size": "1280x720"
  }' \
  --max-time 600 \
  -s 2>&1)

echo "Response: $RESPONSE"

# Check if it's a timeout or actual response
if [[ $RESPONSE == *"created"* ]]; then
  echo "Success! Video generation completed."
  echo "$RESPONSE" | jq .
else
  echo "Failed or timed out after 10 minutes."
  echo "Checking logs for task IDs..."
  docker compose logs api --since 10m 2>&1 | grep "MiniMax video generation task created:" | tail -5
fi