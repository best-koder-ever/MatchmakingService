using MatchmakingService.Data;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki("http://loki:3100", labels: new[]
    {
        new LokiLabel { Key = "app", Value = "matchmaking-service" },
        new LokiLabel { Key = "environment", Value = "development" }
    })
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<MatchmakingService.Services.MatchmakingService>();
builder.Services.AddScoped<IAdvancedMatchingService, AdvancedMatchingService>();
builder.Services.AddScoped<NotificationService>();

// Configure MatchmakingDbContext to use MySQL
builder.Services.AddDbContext<MatchmakingDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 30)), // Replace with your MySQL version
        mySqlOptions => mySqlOptions.EnableRetryOnFailure() // Enable retry on failure
    ));

// Register the UserServiceClient with the YARP gateway as the base address
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    client.BaseAddress = new Uri("http://dejting-yarp:8080"); // YARP gateway address
});

var app = builder.Build();

// Apply migrations on startup
// this is done to ensure that the database is created before the application starts
// and that the database schema is up to date with the model
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
    dbContext.Database.Migrate();
}

// Apply migrations on startup (only in development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
