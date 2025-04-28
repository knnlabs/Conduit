FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet publish ConduitLLM.Http.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY . ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
