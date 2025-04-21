FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy csproj files and restore dependencies
COPY *.sln .
COPY ConduitLLM.Configuration/*.csproj ./ConduitLLM.Configuration/
COPY ConduitLLM.Core/*.csproj ./ConduitLLM.Core/
COPY ConduitLLM.Examples/*.csproj ./ConduitLLM.Examples/
COPY ConduitLLM.Http/*.csproj ./ConduitLLM.Http/
COPY ConduitLLM.Providers/*.csproj ./ConduitLLM.Providers/
COPY ConduitLLM.Tests/*.csproj ./ConduitLLM.Tests/
COPY ConduitLLM.WebUI/*.csproj ./ConduitLLM.WebUI/

# Restore as distinct layers
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o out ConduitLLM.WebUI/ConduitLLM.WebUI.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
# Default to SQLite but allow for PostgreSQL configuration via environment variables
ENV DB_PROVIDER=sqlite
ENV DB_CONNECTION_STRING=Data Source=/data/conduit.db

# Ensure the /data directory exists for SQLite database file
RUN mkdir -p /data

# Expose ports for WebUI (5001, 5002) and API (5000, 5003)
EXPOSE 80 5000 5001 5002 5003

# Set the entry point
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
