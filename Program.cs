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
builder.Services.AddDbContext<MatchmakingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register the UserServiceClient with the YARP gateway as the base address
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    client.BaseAddress = new Uri("http://dejting-yarp:8080"); // YARP gateway address
});

var app = builder.Build();

// Apply migrations on startup (only in development)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            dbContext.Database.Migrate();
            Log.Information("Database migration applied successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations.");
            throw; // Re-throw the exception to prevent the app from starting
        }
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
