# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore packages first (cached when only code changes)
COPY Shipments.Api.csproj .
RUN dotnet restore

# Copy the rest of the code and publish a Release build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Stage 2: run ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Take only the published output from the build stage
COPY --from=build /app/publish .

# .NET 8 container images listen on port 8080 by default
EXPOSE 8080
ENTRYPOINT ["dotnet", "Shipments.Api.dll"]