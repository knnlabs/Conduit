#!/bin/bash
# Test image generation API directly

# Get WebUI virtual key - use the one we just created
WEBUI_KEY="condt_iHDUFt5BCSsOfgxZKqUD5Sjclq70y1yMLAcrb+F/EQU="

echo "Using WebUI virtual key: ${WEBUI_KEY:0:10}..."

# Test async image generation endpoint
echo "Testing async image generation endpoint..."
RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X POST http://localhost:5000/v1/images/generations/async \
  -H "Authorization: Bearer $WEBUI_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A cute robot playing with a kitten",
    "model": "dall-e-3",
    "n": 1,
    "size": "1024x1024",
    "quality": "standard",
    "style": "vivid",
    "response_format": "url"
  }')

HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS:" | cut -d: -f2)
BODY=$(echo "$RESPONSE" | sed '/HTTP_STATUS:/d')

echo "HTTP Status: $HTTP_STATUS"
echo "Response body:"
echo "$BODY" | jq . 2>/dev/null || echo "$BODY"

# If successful, check the status endpoint
if [ "$HTTP_STATUS" = "202" ]; then
    TASK_ID=$(echo "$BODY" | jq -r '.task_id // empty')
    if [ -n "$TASK_ID" ]; then
        echo -e "\nTask created successfully. Task ID: $TASK_ID"
        echo "Checking task status..."
        sleep 2
        
        STATUS_RESPONSE=$(curl -s -X GET "http://localhost:5000/v1/images/generations/$TASK_ID/status" \
          -H "Authorization: Bearer $WEBUI_KEY" \
          -H "Content-Type: application/json")
        
        echo "Task status:"
        echo "$STATUS_RESPONSE" | jq . 2>/dev/null || echo "$STATUS_RESPONSE"
    fi
fi