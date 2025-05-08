# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Conduit.sln .
COPY ConduitLLM.Configuration/*.csproj ./ConduitLLM.Configuration/
COPY ConduitLLM.Core/*.csproj ./ConduitLLM.Core/
COPY ConduitLLM.Providers/*.csproj ./ConduitLLM.Providers/
COPY ConduitLLM.Http/*.csproj ./ConduitLLM.Http/
COPY ConduitLLM.WebUI/*.csproj ./ConduitLLM.WebUI/
# Add other projects referenced by the solution for restore step
COPY ConduitLLM.Examples/*.csproj ./ConduitLLM.Examples/
COPY ConduitLLM.Tests/*.csproj ./ConduitLLM.Tests/

# Restore dependencies
RUN dotnet restore Conduit.sln

# Copy the rest of the source code
COPY . .

# Publish the WebUI project first
WORKDIR /src/ConduitLLM.WebUI
RUN dotnet restore ConduitLLM.WebUI.csproj # Ensure project-specific restore before publish
RUN dotnet publish ConduitLLM.WebUI.csproj -c Release -o /app/publish/webui --no-restore

# Publish the Http API project
WORKDIR /src/ConduitLLM.Http
RUN dotnet restore ConduitLLM.Http.csproj # Ensure project-specific restore before publish
# Temporarily remove conflicting file from WebUI source *before* publishing Http
# The WebUI project is already published correctly with its appsettings.json
RUN rm ../ConduitLLM.WebUI/appsettings.json
RUN dotnet publish ConduitLLM.Http.csproj -c Release -o /app/publish/http --no-restore

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
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
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]

# Optional: Add healthcheck if needed
# HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 CMD curl --fail http://localhost:5000/healthz || exit 1
