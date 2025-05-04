#!/bin/bash

# Script to update HttpClient references in test files
# Run this script from the Conduit root directory

# Ensure the TestHelpers directory exists
mkdir -p ConduitLLM.Tests/TestHelpers

# First create any missing imports in test files
find ConduitLLM.Tests -name "*ClientTests.cs" | xargs -I{} sed -i '/using ConduitLLM.Providers;/a using ConduitLLM.Tests.TestHelpers;' {}
find ConduitLLM.Tests -name "*ClientTests.cs" | xargs -I{} sed -i '/using Microsoft.Extensions.Logging;/a using System.Linq;' {}

# Replace HttpClient with HttpClientFactoryAdapter.AdaptHttpClient
for file in $(find ConduitLLM.Tests -name "*ClientTests.cs"); do
  echo "Processing $file..."
  
  # Replace HttpClient with IHttpClientFactory adapter
  sed -i 's/new [A-Za-z]*Client(\([^,]*\), \([^,]*\), \([^,]*\), _httpClient/var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);\n        new \1Client(\1, \2, \3, httpClientFactory/g' "$file"
  
  # Also replace any direct instancing of clients with _httpClient
  sed -i 's/, _httpClient)/, HttpClientFactoryAdapter.AdaptHttpClient(_httpClient))/g' "$file"
done

echo "Script completed."