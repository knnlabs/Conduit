# --- Database Configuration ---
# For Postgres, set DATABASE_URL in the format:
#   DATABASE_URL=postgresql://user:password@host:port/database
# For SQLite, set CONDUIT_SQLITE_PATH to the desired file path (default: ConduitConfig.db)
# Example (Postgres):
#   ENV DATABASE_URL=postgresql://postgres:yourpassword@yourhost:5432/yourdb
# Example (SQLite):
#   ENV CONDUIT_SQLITE_PATH=/data/ConduitConfig.db

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Only copy directories that are essential for the HTTP build
COPY ["ConduitLLM.Http", "/src/ConduitLLM.Http"]
COPY ["ConduitLLM.Core", "/src/ConduitLLM.Core"]
COPY ["ConduitLLM.Configuration", "/src/ConduitLLM.Configuration"]
COPY ["ConduitLLM.Providers", "/src/ConduitLLM.Providers"]
COPY ["ConduitLLM.WebUI", "/src/ConduitLLM.WebUI"]
# Copy solution file if it exists
COPY ["*.sln", "/src/"]

# List directories for debugging
RUN echo "==== Directory structure ====" && \
    ls -la /src && \
    echo "==== HTTP directory ====" && \
    ls -la /src/ConduitLLM.Http

# Create dummy Common directory if needed
RUN if [ ! -d "/src/ConduitLLM.Common" ]; then \
    echo "Creating missing ConduitLLM.Common directory" && \
    mkdir -p /src/ConduitLLM.Common && \
    echo "namespace ConduitLLM.Common { public class Dummy {} }" > /src/ConduitLLM.Common/Dummy.cs && \
    echo '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>' > /src/ConduitLLM.Common/ConduitLLM.Common.csproj; \
    fi

# Restore and build HTTP project
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj" || \
    (echo "Restore failed - check directory structure" && exit 1)

RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
