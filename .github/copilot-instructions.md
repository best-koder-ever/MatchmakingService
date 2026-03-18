# MatchmakingService
.NET 8 matchmaking and candidate scoring service.
## Build & Test
```bash
dotnet restore MatchmakingService.csproj && dotnet build && dotnet test MatchmakingService.Tests/MatchmakingService.Tests.csproj
```
## Architecture
- Candidate scoring and ranking, swipe processing, match creation
- CQRS via MediatR, EF Core 8 with MySQL
## Rules
- All new code must have unit tests
