using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MatchmakingService.Common;

public class InternalApiKeyAuthFilter : IAuthorizationFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalApiKeyAuthFilter> _logger;

    public InternalApiKeyAuthFilter(IConfiguration configuration, ILogger<InternalApiKeyAuthFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var validApiKeys = _configuration["InternalAuth:ValidApiKeys"]?.Split(',') ?? Array.Empty<string>();
        
        if (validApiKeys.Length == 0)
        {
            _logger.LogWarning("No valid internal API keys configured - allowing request (DEV mode)");
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Internal-API-Key", out var receivedKey))
        {
            _logger.LogWarning("Internal API call missing X-Internal-API-Key header from {RemoteIp}", 
                context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Missing internal API key",
                message = "Service-to-service calls require X-Internal-API-Key header"
            });
            return;
        }

        if (!validApiKeys.Contains(receivedKey.ToString()))
        {
            _logger.LogWarning("Invalid internal API key received from {RemoteIp}", 
                context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Invalid internal API key",
                message = "The provided API key is not authorized for service-to-service calls"
            });
            return;
        }

        _logger.LogDebug("Internal API key validated successfully");
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireInternalApiKeyAttribute : ServiceFilterAttribute
{
    public RequireInternalApiKeyAttribute() : base(typeof(InternalApiKeyAuthFilter))
    {
    }
}
