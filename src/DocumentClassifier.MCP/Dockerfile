# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder
WORKDIR /src

# Copy project files
COPY ["src/DocumentClassifier/DocumentClassifier.csproj", "src/DocumentClassifier/"]
COPY ["src/DocumentClassifier.MCP/DocumentClassifier.MCP.csproj", "src/DocumentClassifier.MCP/"]

# Restore dependencies
RUN dotnet restore "src/DocumentClassifier.MCP/DocumentClassifier.MCP.csproj"

# Copy all source
COPY . .

# Build
WORKDIR "/src/src/DocumentClassifier.MCP"
RUN dotnet build "DocumentClassifier.MCP.csproj" -c Release -o /app/build

# Publish stage
FROM builder AS publish
RUN dotnet publish "DocumentClassifier.MCP.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated9.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=publish ["/app/publish", "/home/site/wwwroot"]
