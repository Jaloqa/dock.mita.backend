using Mita.Api.Extensions;
using Mita.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddMitaLogging();
builder.Services.AddMitaErrorHandling();
builder.Services.AddMitaHealth();
builder.Services.AddMitaMediator();
builder.Services.AddMitaApiVersioning();
builder.Services.AddMitaOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseAuthorization();
app.MapMitaHealth();
app.MapMitaOpenApi();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestEndpoints();
}

await app.RunAsync();

public partial class Program;
