#!/bin/bash
# Script to run just the streaming tests that were fixed

echo "Running fixed streaming tests..."
dotnet test ConduitLLM.Tests --filter "OpenAIClientTests.StreamChatCompletionAsync"