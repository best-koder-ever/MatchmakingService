# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy the project file and restore dependencies
COPY MatchmakingService.csproj ./
RUN dotnet restore

# Copy the entire project and build the application
COPY . ./
RUN dotnet publish MatchmakingService.csproj -c Release -o out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Copy the public key for JWT validation
COPY public.key ./public.key

EXPOSE 8083
ENV ASPNETCORE_URLS=http://*:8083
ENTRYPOINT ["dotnet", "MatchmakingService.dll"]