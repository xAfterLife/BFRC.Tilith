FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + csproj files only (for caching and clean restore)
COPY BFRC.Tilith.sln .
COPY Tilith.Core/Tilith.Core.csproj Tilith.Core/
COPY Tilith.Api/Tilith.Api.csproj Tilith.Api/
COPY Tilith.Bot/Tilith.Bot.csproj Tilith.Bot/
COPY Tilith.Host/Tilith.Host.csproj Tilith.Host/

# Restore whole solution
RUN dotnet restore BFRC.Tilith.sln

# Copy source
COPY . .

# Build
WORKDIR "/src/Tilith.Host"
RUN dotnet build "Tilith.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tilith.Host.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/logs

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Tilith.Host.dll"]