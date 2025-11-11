# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Conduit.sln .
COPY Shared/ConduitLLM.Configuration/*.csproj ./Shared/ConduitLLM.Configuration/
COPY Shared/ConduitLLM.Core/*.csproj ./Shared/ConduitLLM.Core/
COPY Shared/ConduitLLM.Providers/*.csproj ./Shared/ConduitLLM.Providers/
COPY Services/ConduitLLM.Http/*.csproj ./Services/ConduitLLM.Http/
COPY WebAdmin/*.csproj ./WebAdmin/
# Add other projects referenced by the solution for restore step
COPY Tests/ConduitLLM.Tests/*.csproj ./Tests/ConduitLLM.Tests/
COPY Tests/ConduitLLM.IntegrationTests/*.csproj ./Tests/ConduitLLM.IntegrationTests/
COPY Tests/ConduitLLM.Benchmarks/*.csproj ./Tests/ConduitLLM.Benchmarks/

# Restore dependencies
RUN dotnet restore Conduit.sln

# Copy the rest of the source code
COPY . .

# Publish the WebUI project first
WORKDIR /src/WebAdmin
RUN dotnet restore WebAdmin.csproj # Ensure project-specific restore before publish
RUN dotnet publish WebAdmin.csproj -c Release -o /app/publish/webui --no-restore

# Publish the Http API project
WORKDIR /src/Services/ConduitLLM.Http
RUN dotnet restore ConduitLLM.Http.csproj # Ensure project-specific restore before publish
# Temporarily remove conflicting file from WebUI source *before* publishing Http
# The WebUI project is already published correctly with its appsettings.json
RUN rm ../../WebAdmin/appsettings.json
RUN dotnet publish ConduitLLM.Http.csproj -c Release -o /app/publish/http --no-restore

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.2
WORKDIR /app

# Copy published application files from the build stage
# Copy WebUI first, then Http API (Http's appsettings.json will overwrite WebUI's if present)
COPY --from=build /app/publish/webui .
COPY --from=build /app/publish/http .

# Define default environment variables
# These can be overridden at runtime (e.g., via docker-compose.yml or docker run -e)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV DB_PROVIDER=sqlite
# Recommend mounting /data as a volume for persistent storage
ENV CONDUIT_SQLITE_PATH=/data/conduit.db
# Base URL for the HTTP API, used by WebUI. Set to public HTTPS URL in deployment.
ENV CONDUIT_API_BASE_URL=http://localhost:5000

# Expose the port the application listens on
EXPOSE 80

# Set the entrypoint to the WebUI application
ENTRYPOINT ["dotnet", "WebAdmin.dll"]

# Optional: Add healthcheck if needed
# HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 CMD curl --fail http://localhost:5000/healthz || exit 1
