# Build stage - Use .NET 9.0 SDK (10.0 is preview, use stable for production)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["Tilith.Host/Tilith.Host.csproj", "Tilith.Host/"]
COPY ["Tilith.Core/Tilith.Core.csproj", "Tilith.Core/"]
COPY ["Tilith.Bot/Tilith.Bot.csproj", "Tilith.Bot/"]
COPY ["Tilith.Api/Tilith.Api.csproj", "Tilith.Api/"]

# Restore dependencies
RUN dotnet restore "Tilith.Host/Tilith.Host.csproj"

# Copy source code
COPY . .

# Build and publish
WORKDIR "/src/Tilith.Host"
RUN dotnet publish "Tilith.Host.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Runtime stage - Use .NET 9.0 Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks and CA certificates
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN useradd -m -u 1000 tilith && \
    chown -R tilith:tilith /app

# Copy published application
COPY --from=build --chown=tilith:tilith /app/publish .

# Switch to non-root user
USER tilith

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_RUNNING_IN_CONTAINER=true

# Run application
ENTRYPOINT ["dotnet", "Tilith.Host.dll"]