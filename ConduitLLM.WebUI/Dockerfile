# --- ConduitLLM WebUI Docker Image ---
# WebUI communicates with Admin API and HTTP API - no direct database access

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Only copy directories that are essential for the WebUI build
COPY ["ConduitLLM.WebUI", "/src/ConduitLLM.WebUI"]
COPY ["ConduitLLM.Core", "/src/ConduitLLM.Core"]
COPY ["ConduitLLM.Configuration", "/src/ConduitLLM.Configuration"]
COPY ["ConduitLLM.Providers", "/src/ConduitLLM.Providers"]
# Copy solution file if it exists
COPY ["*.sln", "/src/"]

# List directories for debugging
RUN echo "==== Directory structure ====" && \
    ls -la /src && \
    echo "==== WebUI directory ====" && \
    ls -la /src/ConduitLLM.WebUI

# Create dummy Common directory if needed
RUN if [ ! -d "/src/ConduitLLM.Common" ]; then \
    echo "Creating missing ConduitLLM.Common directory" && \
    mkdir -p /src/ConduitLLM.Common && \
    echo "namespace ConduitLLM.Common { public class Dummy {} }" > /src/ConduitLLM.Common/Dummy.cs && \
    echo '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>' > /src/ConduitLLM.Common/ConduitLLM.Common.csproj; \
    fi

# Restore and build WebUI project
RUN dotnet restore "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" || \
    (echo "Restore failed - check directory structure" && exit 1)

RUN dotnet publish "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
