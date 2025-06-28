#!/bin/bash

# Test the show-virtual-key functionality
echo "Testing conduit-tui --show-virtual-key parameter..."
echo ""

# Run the command with the alpha master key
./conduit-tui --master-key alpha --show-virtual-key

# Note: This assumes the Admin API is running on localhost:5002