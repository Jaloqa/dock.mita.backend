using Mita.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddMitaLogging();
builder.Services.AddMitaHealth();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapMitaHealth();

await app.RunAsync();

public partial class Program;
