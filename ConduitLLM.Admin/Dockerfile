FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Install curl for health checks
RUN apt-get update && apt-get install -y curl

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ConduitLLM.Admin/ConduitLLM.Admin.csproj", "ConduitLLM.Admin/"]
COPY ["ConduitLLM.Configuration/ConduitLLM.Configuration.csproj", "ConduitLLM.Configuration/"]
COPY ["ConduitLLM.Core/ConduitLLM.Core.csproj", "ConduitLLM.Core/"]
COPY ["ConduitLLM.Providers/ConduitLLM.Providers.csproj", "ConduitLLM.Providers/"]
COPY ["ConduitLLM.Security/ConduitLLM.Security.csproj", "ConduitLLM.Security/"]
RUN dotnet restore "ConduitLLM.Admin/ConduitLLM.Admin.csproj"
COPY . .
WORKDIR "/src/ConduitLLM.Admin"
RUN dotnet build "ConduitLLM.Admin.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConduitLLM.Admin.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConduitLLM.Admin.dll"]