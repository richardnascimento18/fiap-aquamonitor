FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AquaMonitor.API/AquaMonitor.API.csproj AquaMonitor.API/
RUN dotnet restore AquaMonitor.API/AquaMonitor.API.csproj

COPY AquaMonitor.API/ AquaMonitor.API/
WORKDIR /src/AquaMonitor.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install SQLite native library (required by P/Invoke wrapper)
RUN apt-get update && apt-get install -y libsqlite3-dev && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ConnectionStrings__DefaultConnection=/app/data/aquamonitor.db

RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "AquaMonitor.API.dll"]
