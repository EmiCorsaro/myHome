using System.Text.Json.Serialization;
using MyHome.Api.Endpoints;
using MyHome.Api.ErrorHandling;
using MyHome.Api.Services;
using MyHome.Api.Tenancy;
using MyHome.Modules.Ledger;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared;
using MyHome.Modules.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSharedModule();
builder.Services.AddLedgerModule();
builder.Services.AddApiServices();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<ClientDisconnectedExceptionHandler>();

builder.Services.AddExceptionHandler<ValidationFailedExceptionHandler>();

const string DevelopmentCorsPolicy = "development-spa";
builder.Services.AddCors(options => options.AddPolicy(
    DevelopmentCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentHouseholdResolver();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

await SharedSchema.MigrateAsync(app.Services).ConfigureAwait(false);
await LedgerSchema.MigrateAsync(app.Services).ConfigureAwait(false);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevelopmentCorsPolicy);

    var householdId = await DevelopmentSeeder.EnsureSeededAsync(app.Services)
        .ConfigureAwait(false);

    await LedgerSeeder.EnsureSeededAsync(app.Services, householdId).ConfigureAwait(false);
}

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapDefaultEndpoints();
app.MapHouseholdEndpoints();
app.MapLedgerEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
