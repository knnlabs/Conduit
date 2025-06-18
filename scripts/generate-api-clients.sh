#!/bin/bash

# Script to generate API client libraries from OpenAPI specification
# Requires: npm install -g @openapitools/openapi-generator-cli

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OPENAPI_SPEC="$PROJECT_ROOT/ConduitLLM.Http/openapi.yaml"
OUTPUT_DIR="$PROJECT_ROOT/Clients/generated"

echo "Generating API clients from OpenAPI specification..."
echo "OpenAPI spec: $OPENAPI_SPEC"
echo "Output directory: $OUTPUT_DIR"

# Check if OpenAPI Generator is installed
if ! command -v openapi-generator-cli &> /dev/null; then
    echo "Error: OpenAPI Generator CLI is not installed."
    echo "Please install it with: npm install -g @openapitools/openapi-generator-cli"
    exit 1
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Generate TypeScript/JavaScript client
echo "Generating TypeScript client..."
openapi-generator-cli generate \
    -i "$OPENAPI_SPEC" \
    -g typescript-axios \
    -o "$OUTPUT_DIR/typescript" \
    --additional-properties=npmName=@conduit/api-client,npmVersion=1.0.0,supportsES6=true,withSeparateModelsAndApi=true

# Generate Python client
echo "Generating Python client..."
openapi-generator-cli generate \
    -i "$OPENAPI_SPEC" \
    -g python \
    -o "$OUTPUT_DIR/python" \
    --additional-properties=packageName=conduit_client,projectName=conduit-api-client,packageVersion=1.0.0

# Generate C# client
echo "Generating C# client..."
openapi-generator-cli generate \
    -i "$OPENAPI_SPEC" \
    -g csharp-netcore \
    -o "$OUTPUT_DIR/csharp" \
    --additional-properties=packageName=Conduit.ApiClient,packageVersion=1.0.0,targetFramework=net6.0

# Generate Go client
echo "Generating Go client..."
openapi-generator-cli generate \
    -i "$OPENAPI_SPEC" \
    -g go \
    -o "$OUTPUT_DIR/go" \
    --additional-properties=packageName=conduit,isGoSubmodule=true

# Generate Java client
echo "Generating Java client..."
openapi-generator-cli generate \
    -i "$OPENAPI_SPEC" \
    -g java \
    -o "$OUTPUT_DIR/java" \
    --additional-properties=groupId=com.conduit,artifactId=conduit-api-client,artifactVersion=1.0.0

echo "Client generation complete!"
echo "Generated clients are available in: $OUTPUT_DIR"
echo ""
echo "Next steps:"
echo "1. Review generated code in $OUTPUT_DIR"
echo "2. Add custom documentation and examples"
echo "3. Publish to respective package managers"