using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SlateIQ.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/health", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTimeOffset.UtcNow,
            region = Environment.GetEnvironmentVariable("SLATEIQ_REGION") ?? "IN",
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
        }))
        .WithName("GetHealth")
        .WithTags("Health")
        .AllowAnonymous();
    }
}
