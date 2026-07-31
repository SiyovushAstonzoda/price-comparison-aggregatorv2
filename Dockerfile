# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PriceAggregator.slnx", "."]
COPY ["PriceAggregator.Api/", "PriceAggregator.Api/"]
COPY ["PriceAggregator.Core/", "PriceAggregator.Core/"]
COPY ["PriceAggregator.Scraper/", "PriceAggregator.Scraper/"]
COPY ["PriceAggregator.Worker/", "PriceAggregator.Worker/"]

RUN dotnet restore "PriceAggregator.slnx"
RUN dotnet build "PriceAggregator.slnx" -c Release -o /app/build

# API Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api-runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD dotnet --version || exit 1
COPY --from=build /app/build/PriceAggregator.Api .
ENTRYPOINT ["dotnet", "PriceAggregator.Api.dll"]

# Worker Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker-runtime
WORKDIR /app
COPY --from=build /app/build/PriceAggregator.Worker .
ENTRYPOINT ["dotnet", "PriceAggregator.Worker.dll"]

# Scraper Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS scraper-runtime
WORKDIR /app
COPY --from=build /app/build/PriceAggregator.Scraper .
ENTRYPOINT ["dotnet", "PriceAggregator.Scraper.dll"]
