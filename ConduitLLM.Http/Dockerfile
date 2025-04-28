FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything first
COPY . .

# Restore dependencies
RUN dotnet restore "ConduitLLM.Http/ConduitLLM.Http.csproj"

# Build and publish
RUN dotnet publish "ConduitLLM.Http/ConduitLLM.Http.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ConduitLLM.Http.dll"]
