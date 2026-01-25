using DatingApp.Shared.Middleware;
using MatchmakingService.Data;
using MatchmakingService.Extensions;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.GrafanaLoki(context.Configuration["Serilog:LokiUrl"] ?? "http://loki:3100", labels: new[]
        {
            new LokiLabel { Key = "app", Value = "MatchmakingService" },
            new LokiLabel { Key = "environment", Value = context.HostingEnvironment.EnvironmentName }
        });
});

builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<MatchmakingService.Services.MatchmakingService>();
builder.Services.AddScoped<IAdvancedMatchingService, AdvancedMatchingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient();

// Register scoring configuration
var scoringConfig = new MatchmakingService.Models.ScoringConfiguration();
builder.Configuration.GetSection("Scoring").Bind(scoringConfig);
builder.Services.AddSingleton(scoringConfig);

// Register daily suggestion limits configuration
builder.Services.Configure<MatchmakingService.Models.DailySuggestionLimits>(
    builder.Configuration.GetSection("DailySuggestionLimits"));
builder.Services.AddSingleton<IDailySuggestionTracker, InMemoryDailySuggestionTracker>();

builder.Services.AddCorrelationIds();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("MatchmakingService requires a configured DefaultConnection connection string.");
}

builder.Services.AddDbContext<MatchmakingDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 30)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    ));

builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://dejting-yarp:8080");
});

builder.Services.AddHttpClient<ISafetyServiceClient, SafetyServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://dejting-yarp:8080");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCorrelationIds();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
