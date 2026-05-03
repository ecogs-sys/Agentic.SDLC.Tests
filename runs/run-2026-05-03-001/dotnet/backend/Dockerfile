# syntax=docker/dockerfile:1

# ── Stage 1: build / publish ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first so NuGet restore is cached independently of source changes.
COPY ContactApp.Api/ContactApp.Api.csproj ContactApp.Api/
RUN dotnet restore ContactApp.Api/ContactApp.Api.csproj

# Copy the rest of the source and publish a framework-dependent release build.
COPY ContactApp.Api/ ContactApp.Api/
RUN dotnet publish ContactApp.Api/ContactApp.Api.csproj \
    --configuration Release \
    --output /app/publish

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published output from the build stage.
COPY --from=build /app/publish .

# Expose port 8080 and configure ASP.NET Core to listen on it by default.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ContactApp.Api.dll"]
