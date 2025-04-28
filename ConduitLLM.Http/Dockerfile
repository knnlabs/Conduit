FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only necessary projects for building HTTP API
COPY ["ConduitLLM.Http/", "/src/ConduitLLM.Http/"]
COPY ["ConduitLLM.Core/", "/src/ConduitLLM.Core/"] 
COPY ["ConduitLLM.Common/", "/src/ConduitLLM.Common/"]
COPY ["ConduitLLM.Configuration/", "/src/ConduitLLM.Configuration/"]
COPY ["ConduitLLM.Providers/", "/src/ConduitLLM.Providers/"]
COPY ["ConduitLLM.sln", "/src/"]

# Restore and build HTTP project only
WORKDIR /src
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj"
RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
