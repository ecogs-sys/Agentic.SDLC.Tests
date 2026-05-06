# CONFLICT: Story specifies mcr.microsoft.com/dotnet/sdk:8.0 and mcr.microsoft.com/dotnet/aspnet:8.0,
# but the project targets net10.0 (ContactApp.Api.csproj uses <TargetFramework>net10.0</TargetFramework>).
# SDK 8.0 cannot build a net10.0 project; .NET 10 images are used instead to produce a working artifact.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Api/ContactApp.Api.csproj Api/
RUN dotnet restore Api/ContactApp.Api.csproj
COPY . .
RUN dotnet publish Api/ContactApp.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ContactApp.Api.dll"]
