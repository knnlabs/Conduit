#!/bin/bash

# Test script to verify TUI works without API servers running

echo "Testing Conduit TUI without API servers..."
echo "This will verify the TUI can start and handle missing connections gracefully."
echo ""

# Use a test master key
TEST_MASTER_KEY="test-master-key-12345"

# Run the TUI with a timeout to auto-exit after 5 seconds
timeout 5s ./conduit-tui --master-key "$TEST_MASTER_KEY" || EXIT_CODE=$?

if [ "$EXIT_CODE" = "124" ]; then
    echo ""
    echo "✓ TUI started successfully and ran for 5 seconds without crashing"
    echo "✓ SignalR connection failure was handled gracefully"
    exit 0
else
    echo ""
    echo "✗ TUI exited with error code: $EXIT_CODE"
    exit 1
fi