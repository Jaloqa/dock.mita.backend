using Mita.Api.Extensions;
using Mita.Api.Features.System;
using Mita.Api.Middleware;
using Mita.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddMitaLogging();
builder.Services.AddMitaErrorHandling();
builder.Services.AddMitaHealth();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMitaMediator();
builder.Services.AddMitaAuth();
builder.Services.AddMitaApiVersioning();
builder.Services.AddMitaOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapMitaHealth();
app.MapMitaOpenApi();
app.MapSystemEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestEndpoints();
}

await app.RunAsync();

public partial class Program;
