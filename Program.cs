using MatchmakingService.Data;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki("http://loki:3100", labels: new[]
    {
        new LokiLabel { Key = "app", Value = "MatchmakingService" },
        new LokiLabel { Key = "environment", Value = "development" }
    })
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT Authentication
var isDemoModeForAuth = Environment.GetEnvironmentVariable("DEMO_MODE") == "true";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = GetPublicKey()
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<MatchmakingService.Services.MatchmakingService>();
builder.Services.AddScoped<IAdvancedMatchingService, AdvancedMatchingService>();
builder.Services.AddScoped<NotificationService>();

// Configure MatchmakingDbContext conditionally
if (Environment.GetEnvironmentVariable("DEMO_MODE") == "true")
{
    builder.Services.AddDbContext<MatchmakingDbContext>(options =>
        options.UseInMemoryDatabase("MatchmakingServiceDemo"));
    Console.WriteLine("MatchmakingService using in-memory database for demo mode");
}
else
{
    builder.Services.AddDbContext<MatchmakingDbContext>(options =>
        options.UseMySql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            new MySqlServerVersion(new Version(8, 0, 30)), // Replace with your MySQL version
            mySqlOptions => mySqlOptions.EnableRetryOnFailure() // Enable retry on failure
        ));
    Console.WriteLine("MatchmakingService using MySQL database for production mode");
}

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
    
    // Only migrate if using a relational database (not in-memory)
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
        Console.WriteLine("MatchmakingService: Applied database migrations");
    }
    else
    {
        Console.WriteLine("MatchmakingService: Using in-memory database, skipping migrations");
    }
}

// Apply migrations on startup (only in development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Add health check endpoint
app.MapGet("/health", async context =>
{
    await context.Response.WriteAsync("Healthy");
});

app.Run();

// ================================
// RSA KEY MANAGEMENT
// Public key validation for JWT tokens from AuthService
// ================================

static RsaSecurityKey GetPublicKey()
{
    try
    {
        var publicKeyPath = "public.key";
        if (File.Exists(publicKeyPath))
        {
            var publicKeyPem = File.ReadAllText(publicKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return new RsaSecurityKey(rsa);
        }
        else
        {
            // For demo mode or when no key file exists, create a temporary key
            // In production, this should always use the proper public key
            var rsa = RSA.Create(2048);
            return new RsaSecurityKey(rsa);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading public key: {ex.Message}");
        // Fallback to temporary key
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa);
    }
}
