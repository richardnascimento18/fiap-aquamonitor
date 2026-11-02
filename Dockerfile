FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY global.json NuGet.config Directory.Build.props ./
COPY AquaMonitor.API/AquaMonitor.API.csproj AquaMonitor.API/packages.lock.json AquaMonitor.API/
COPY AquaMonitor.Domain/AquaMonitor.Domain.csproj AquaMonitor.Domain/packages.lock.json AquaMonitor.Domain/
COPY AquaMonitor.Application/AquaMonitor.Application.csproj AquaMonitor.Application/packages.lock.json AquaMonitor.Application/
COPY AquaMonitor.Infrastructure/AquaMonitor.Infrastructure.csproj AquaMonitor.Infrastructure/packages.lock.json AquaMonitor.Infrastructure/
RUN dotnet restore AquaMonitor.API/AquaMonitor.API.csproj --locked-mode
COPY AquaMonitor.API/ AquaMonitor.API/
COPY AquaMonitor.Domain/ AquaMonitor.Domain/
COPY AquaMonitor.Application/ AquaMonitor.Application/
COPY AquaMonitor.Infrastructure/ AquaMonitor.Infrastructure/
RUN dotnet publish AquaMonitor.API/AquaMonitor.API.csproj --configuration Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection=/app/data/aquamonitor.db
RUN mkdir -p /app/data && chown app:app /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "AquaMonitor.API.dll"]
