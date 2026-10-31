FROM mcr.microsoft.com/dotnet/sdk:8.0.427 AS build
WORKDIR /src
COPY global.json NuGet.config Directory.Build.props ./
COPY AquaMonitor.API/AquaMonitor.API.csproj AquaMonitor.API/packages.lock.json AquaMonitor.API/
RUN dotnet restore AquaMonitor.API/AquaMonitor.API.csproj --locked-mode
COPY AquaMonitor.API/ AquaMonitor.API/
RUN dotnet publish AquaMonitor.API/AquaMonitor.API.csproj --configuration Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0.33 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection=/app/data/aquamonitor.db
RUN mkdir -p /app/data && chown app:app /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "AquaMonitor.API.dll"]
