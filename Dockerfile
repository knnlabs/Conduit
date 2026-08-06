# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-wide restore inputs and project files first for layer caching
COPY Conduit.slnx Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY Shared/ConduitLLM.Configuration/*.csproj ./Shared/ConduitLLM.Configuration/
COPY Shared/ConduitLLM.Core/*.csproj ./Shared/ConduitLLM.Core/
COPY Shared/ConduitLLM.Functions/*.csproj ./Shared/ConduitLLM.Functions/
COPY Shared/ConduitLLM.Providers/*.csproj ./Shared/ConduitLLM.Providers/
COPY Shared/ConduitLLM.Security/*.csproj ./Shared/ConduitLLM.Security/
COPY Services/ConduitLLM.Admin/*.csproj ./Services/ConduitLLM.Admin/
COPY Services/ConduitLLM.Gateway/*.csproj ./Services/ConduitLLM.Gateway/
COPY Tests/ConduitLLM.Tests/*.csproj ./Tests/ConduitLLM.Tests/
COPY Tests/ConduitLLM.IntegrationTests/*.csproj ./Tests/ConduitLLM.IntegrationTests/
COPY Tests/ConduitLLM.Benchmarks/*.csproj ./Tests/ConduitLLM.Benchmarks/
COPY Tests/ConduitLLM.BillingInvariantTests/*.csproj ./Tests/ConduitLLM.BillingInvariantTests/
COPY Tests/ConduitLLM.FaultInjectionTests/*.csproj ./Tests/ConduitLLM.FaultInjectionTests/
COPY tools/openapi/GenerateOpenApiSpecs/*.csproj ./tools/openapi/GenerateOpenApiSpecs/

# Restore dependencies
RUN dotnet restore Conduit.slnx

# Copy the rest of the source code
COPY . .

# Publish the WebAdmin project first
WORKDIR /src/WebAdmin
RUN dotnet restore WebAdmin.csproj # Ensure project-specific restore before publish
RUN dotnet publish WebAdmin.csproj -c Release -o /app/publish/webadmin --no-restore

# Publish the Http API project
WORKDIR /src/Services/ConduitLLM.Gateway
RUN dotnet restore ConduitLLM.Gateway.csproj # Ensure project-specific restore before publish
# Temporarily remove conflicting file from WebAdmin source *before* publishing Http
# The WebAdmin project is already published correctly with its appsettings.json
RUN rm ../../WebAdmin/appsettings.json
RUN dotnet publish ConduitLLM.Gateway.csproj -c Release -o /app/publish/http --no-restore

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Copy published application files from the build stage
# Copy WebAdmin first, then Http API (Http's appsettings.json will overwrite WebAdmin's if present)
COPY --from=build /app/publish/webadmin .
COPY --from=build /app/publish/http .

# Define default environment variables
# These can be overridden at runtime (e.g., via docker-compose.yml or docker run -e)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV DB_PROVIDER=sqlite
# Recommend mounting /data as a volume for persistent storage
ENV CONDUIT_SQLITE_PATH=/data/conduit.db
# Base URL for the HTTP API, used by WebAdmin. Set to public HTTPS URL in deployment.
ENV CONDUIT_API_BASE_URL=http://localhost:5000

# Expose the port the application listens on
EXPOSE 80

# Set the entrypoint to the WebAdmin application
ENTRYPOINT ["dotnet", "WebAdmin.dll"]

# Optional: Add healthcheck if needed
# HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 CMD curl --fail http://localhost:5000/healthz || exit 1
