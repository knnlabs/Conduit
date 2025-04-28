FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# First copy everything in separate commands to handle missing directories gracefully
COPY ["ConduitLLM.WebUI", "/src/ConduitLLM.WebUI"]
COPY ["ConduitLLM.Core", "/src/ConduitLLM.Core"]
COPY ["ConduitLLM.Common", "/src/ConduitLLM.Common"]
COPY ["ConduitLLM.Configuration", "/src/ConduitLLM.Configuration"]
COPY ["ConduitLLM.Providers", "/src/ConduitLLM.Providers"]
COPY ["*.sln", "/src/"]

# List what was actually copied (for debugging)
RUN ls -la /src && ls -la /src/ConduitLLM.WebUI || echo "WebUI directory listing failed"

# Restore and build WebUI project
RUN dotnet restore "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" || \
    (echo "Restore failed - check directory structure" && exit 1)

RUN dotnet publish "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
