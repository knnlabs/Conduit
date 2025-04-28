FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only necessary projects for building WebUI
COPY ["ConduitLLM.WebUI/", "/src/ConduitLLM.WebUI/"]
COPY ["ConduitLLM.Core/", "/src/ConduitLLM.Core/"]
COPY ["ConduitLLM.Common/", "/src/ConduitLLM.Common/"]
COPY ["ConduitLLM.Configuration/", "/src/ConduitLLM.Configuration/"]
COPY ["ConduitLLM.Providers/", "/src/ConduitLLM.Providers/"]
COPY ["ConduitLLM.sln", "/src/"]

# Restore and build WebUI project only
WORKDIR /src
RUN dotnet restore "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj"
RUN dotnet publish "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.WebUI.dll"]
