FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution file and project files first
COPY ["ConduitLLM.sln", "."]
COPY ["ConduitLLM.Http/ConduitLLM.Http.csproj", "ConduitLLM.Http/"]
COPY ["ConduitLLM.Common/ConduitLLM.Common.csproj", "ConduitLLM.Common/"]
COPY ["ConduitLLM.Configuration/ConduitLLM.Configuration.csproj", "ConduitLLM.Configuration/"]

# Restore dependencies
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj"

# Copy the rest of the source code
COPY ["ConduitLLM.Http/", "ConduitLLM.Http/"]
COPY ["ConduitLLM.Common/", "ConduitLLM.Common/"]
COPY ["ConduitLLM.Configuration/", "ConduitLLM.Configuration/"]

# Build and publish
RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
