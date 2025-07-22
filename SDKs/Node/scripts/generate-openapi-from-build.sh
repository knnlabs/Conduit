#!/bin/bash

echo "🔨 Building .NET projects to generate OpenAPI specs..."

# Build the projects
cd /home/nbn/Code/Conduit
dotnet build ConduitLLM.Http/ConduitLLM.Http.csproj
dotnet build ConduitLLM.Admin/ConduitLLM.Admin.csproj

# Generate OpenAPI spec for Admin API using Swashbuckle CLI
echo "📄 Generating OpenAPI spec for Admin API..."
dotnet tool install -g Swashbuckle.AspNetCore.Cli --version 8.1.1
dotnet swagger tofile --output ConduitLLM.Admin/openapi-admin.json ConduitLLM.Admin/bin/Debug/net9.0/ConduitLLM.Admin.dll v1

# The Core API already has openapi-core.json
echo "✅ OpenAPI specs ready!"
echo "   - Core API: ConduitLLM.Http/openapi-core.json"
echo "   - Admin API: ConduitLLM.Admin/openapi-admin.json"

# Generate TypeScript types
cd SDKs/Node/scripts
npm run generate:from-files