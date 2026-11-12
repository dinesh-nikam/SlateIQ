using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SlateIQ.Api.Endpoints;
using SlateIQ.Api.Middleware;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Ops;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Application.Abstractions.Security;
using SlateIQ.Application.Tenancy;
using SlateIQ.Infrastructure.Audit;
using SlateIQ.Infrastructure.Ops;
using SlateIQ.Infrastructure.Persistence;
using SlateIQ.Infrastructure.Security;
using SlateIQ.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

// Configuration & Secrets
var connectionString = builder.Configuration.GetConnectionString("SlateIQDb") 
    ?? "Host=localhost;Port=5432;Database=slateiq;Username=slateiq_app;Password=slateiq_app_dev_pw";

// Core Services & Persistence
builder.Services.AddSingleton<ITenantContext, TenantContext>();
builder.Services.AddSingleton(sp =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    return dataSourceBuilder.Build();
});

// Audit & Compliance (MOD-12)
builder.Services.AddScoped<IAuditWriter, HashChainedAuditWriter>();

// Ops & Messaging (MOD-15)
builder.Services.AddScoped<IOutboxPublisher, PostgresOutboxPublisher>();

// Security Services (MOD-02)
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddScoped<ISessionManager, SessionManager>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

// Tenancy Orchestration (MOD-01)
builder.Services.AddScoped<IOrganizationProvisioningSaga, OrganizationProvisioningSaga>();

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
app.MapAuthEndpoints();
app.MapProductionEndpoints();
app.MapBudgetEndpoints();
app.MapVendorEndpoints();
app.MapExpenseEndpoints();
app.MapAuditEndpoints();
app.MapAnalyticsEndpoints();
app.MapDocumentEndpoints();
app.MapExcelEndpoints();

app.Run();
