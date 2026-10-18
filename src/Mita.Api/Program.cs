using Mita.Api.Authentication;
using Mita.Api.Extensions;
using Mita.Api.Features.Accounts;
using Mita.Api.Features.Auth;
using Mita.Api.Features.Rates;
using Mita.Api.Features.Settings;
using Mita.Api.Features.System;
using Mita.Api.Features.Transfers;
using Mita.Api.Features.Users;
using Mita.Api.Middleware;
using Mita.Api.Testing;
using Mita.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddMitaLogging();
builder.Services.AddMitaErrorHandling();
builder.Services.AddMitaHealth();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMitaMediator();
builder.Services.AddMitaAuth();
builder.Services.AddMitaForwardedHeaders();
builder.Services.AddMitaCors();
builder.Services.AddMitaRateLimiting();
builder.Services.AddMitaJson();
builder.Services.AddMitaApiVersioning();
builder.Services.AddMitaOpenApi();

var app = builder.Build();

await app.ApplyMitaMigrationsAsync();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMitaRequestLogging();
app.UseExceptionHandler();
app.UseMitaStatusCodePages();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<XsrfMiddleware>();
app.UseRateLimiter();
app.MapMitaHealth();
app.MapMitaOpenApi();
app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapSettingsEndpoints();
app.MapRatesEndpoints();
app.MapAccountsEndpoints();
app.MapTransfersEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestEndpoints();
}

await app.RunAsync();

public partial class Program;
