FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution file and project files first
COPY ["ConduitLLM.sln", "."]
COPY ["ConduitLLM.WebUI/ConduitLLM.WebUI.csproj", "ConduitLLM.WebUI/"]
COPY ["ConduitLLM.Common/ConduitLLM.Common.csproj", "ConduitLLM.Common/"]
COPY ["ConduitLLM.Configuration/ConduitLLM.Configuration.csproj", "ConduitLLM.Configuration/"]

# Restore dependencies
RUN dotnet restore "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj"

# Copy the rest of the source code
COPY ["ConduitLLM.WebUI/", "ConduitLLM.WebUI/"]
COPY ["ConduitLLM.Common/", "ConduitLLM.Common/"]
COPY ["ConduitLLM.Configuration/", "ConduitLLM.Configuration/"]

# Build and publish
RUN dotnet publish "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
