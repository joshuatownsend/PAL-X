FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY dotnet/Pal.sln .
COPY dotnet/Directory.Build.props .
COPY dotnet/src/Pal.Engine/Pal.Engine.csproj src/Pal.Engine/
COPY dotnet/src/Pal.Ingestion/Pal.Ingestion.csproj src/Pal.Ingestion/
COPY dotnet/src/Pal.Packs/Pal.Packs.csproj src/Pal.Packs/
COPY dotnet/src/Pal.Reporting/Pal.Reporting.csproj src/Pal.Reporting/
COPY dotnet/src/Pal.Application/Pal.Application.csproj src/Pal.Application/
COPY dotnet/src/Pal.Persistence/Pal.Persistence.csproj src/Pal.Persistence/
COPY dotnet/src/Pal.Api/Pal.Api.csproj src/Pal.Api/
COPY dotnet/src/Pal.Cli/Pal.Cli.csproj src/Pal.Cli/

RUN dotnet restore src/Pal.Api/Pal.Api.csproj

COPY dotnet/src/ src/
RUN dotnet publish src/Pal.Api/Pal.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is needed for the Docker Compose healthcheck; aspnet:8.0 (Debian slim) does not include it.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Pack definitions bundled into image at /app/packs/thresholds
COPY packs/thresholds /app/packs/thresholds

ENV ASPNETCORE_URLS=http://+:8080
ENV Storage__LocalRoot=/data/storage
ENV Packs__Directory=/app/packs/thresholds

VOLUME /data/storage

EXPOSE 8080
ENTRYPOINT ["./pal-api"]
