# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy MatchmakingService.csproj
COPY ["matchmaking-service/src/MatchmakingService/MatchmakingService.csproj", "matchmaking-service/src/MatchmakingService/"]

# Copy AuthService.csproj to the location expected by MatchmakingService.csproj's ProjectReference
# ProjectReference from matchmaking-service/src/MatchmakingService/MatchmakingService.csproj is ../../auth-service/AuthService/AuthService.csproj
# This resolves to /app/matchmaking-service/auth-service/AuthService/ when MatchmakingService.csproj is at /app/matchmaking-service/src/MatchmakingService/
COPY ["auth-service/src/AuthService/AuthService.csproj", "matchmaking-service/auth-service/AuthService/"]

# Restore dependencies for MatchmakingService
# This should now find AuthService.csproj at the location specified by the ProjectReference
RUN dotnet restore "matchmaking-service/src/MatchmakingService/MatchmakingService.csproj"

# Copy all source code for MatchmakingService
COPY ["matchmaking-service/src/", "matchmaking-service/src/"]

# Copy all source code for AuthService to the location where its .csproj was placed and restored
COPY ["auth-service/src/AuthService/", "matchmaking-service/auth-service/AuthService/"]

# Copy public key for matchmaking-service.
# It's originally at matchmaking-service/public.key.
# We place it in /app/matchmaking-service/ for the build stage.
COPY ["matchmaking-service/public.key", "matchmaking-service/public.key"]

# Exclude appsettings files from the publish output
RUN rm -f /app/matchmaking-service/auth-service/AuthService/appsettings.json /app/matchmaking-service/auth-service/AuthService/appsettings.Development.json /app/matchmaking-service/src/MatchmakingService/appsettings.json /app/matchmaking-service/src/MatchmakingService/appsettings.Development.json

# Build and publish the MatchmakingService application
RUN dotnet publish "matchmaking-service/src/MatchmakingService/MatchmakingService.csproj" -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .
# Copy public key to the app's root in the runtime image from its location in the build stage.
COPY --from=build-env /app/matchmaking-service/public.key ./public.key

EXPOSE 8080
ENTRYPOINT ["dotnet", "MatchmakingService.dll"]