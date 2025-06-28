#!/bin/bash

echo "Testing Admin API with master key..."

# Direct API test
echo -e "\n1. Testing direct API call:"
curl -s -X GET "http://localhost:5002/api/GlobalSettings/by-key/WebUI_VirtualKey" \
  -H "X-Master-Key: alpha" \
  -H "Accept: application/json" | jq '.'

# Test with dotnet run
echo -e "\n2. Testing with dotnet run:"
cd /home/nbn/Code/Conduit
dotnet run --project ConduitLLM.TUI -- --master-key=alpha --show-virtual-key