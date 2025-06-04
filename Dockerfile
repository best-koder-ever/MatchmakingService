# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy MatchmakingService.csproj (now at the root of matchmaking-service)
COPY ["matchmaking-service/MatchmakingService.csproj", "matchmaking-service/"]

# Restore dependencies for MatchmakingService
RUN dotnet restore "matchmaking-service/MatchmakingService.csproj"

# Copy all source code for MatchmakingService
COPY ["matchmaking-service/", "matchmaking-service/"]

# Copy public key for matchmaking-service.
COPY ["matchmaking-service/public.key", "matchmaking-service/public.key"]

# Exclude appsettings files from the publish output (optional, if needed)
RUN rm -f /app/matchmaking-service/appsettings.json /app/matchmaking-service/appsettings.Development.json

# Build and publish the MatchmakingService application
RUN dotnet publish "matchmaking-service/MatchmakingService.csproj" -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=build-env /app/matchmaking-service/public.key ./public.key

EXPOSE 8080
ENTRYPOINT ["dotnet", "MatchmakingService.dll"]