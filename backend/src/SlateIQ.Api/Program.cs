using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SlateIQ.Api.Endpoints;
using SlateIQ.Api.Middleware;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Infrastructure.Audit;
using SlateIQ.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Configuration & Secrets
var connectionString = builder.Configuration.GetConnectionString("SlateIQDb") 
    ?? "Host=localhost;Port=5432;Database=slateiq;Username=slateiq_app;Password=slateiq_app_dev_pw";

// Core Services
builder.Services.AddSingleton<ITenantContext, TenantContext>();
builder.Services.AddSingleton(sp =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    return dataSourceBuilder.Build();
});
builder.Services.AddScoped<IAuditWriter, HashChainedAuditWriter>();

// ProblemDetails RFC 9457
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["correlationId"] = 
            ctx.HttpContext.Response.Headers["X-Correlation-ID"].ToString();
    };
});

// Authentication & Authorization (Base)
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// Pipeline Middleware
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<TenantContextMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Route Endpoints
app.MapHealthEndpoints();

app.Run();
