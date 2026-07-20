# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder
WORKDIR /src

# Copy project files
COPY ["src/DocumentClassifier/DocumentClassifier.csproj", "src/DocumentClassifier/"]
COPY ["src/DocumentClassifier.MCP/DocumentClassifier.MCP.csproj", "src/DocumentClassifier.MCP/"]

# Restore dependencies
RUN dotnet restore "src/DocumentClassifier/DocumentClassifier.csproj"

# Copy all source
COPY . .

# Build
WORKDIR "/src/src/DocumentClassifier"
RUN dotnet build "DocumentClassifier.csproj" -c Release -o /app/build

# Publish stage
FROM builder AS publish
RUN dotnet publish "DocumentClassifier.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=publish /app/publish .

# Create documents directory
RUN mkdir -p /app/documents

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "DocumentClassifier.dll"]
