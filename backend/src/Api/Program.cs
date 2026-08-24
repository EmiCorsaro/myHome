using System.Text.Json.Serialization;
using MyHome.Api.Endpoints;
using MyHome.Api.ErrorHandling;
using MyHome.Api.Tenancy;
using MyHome.Modules.Ledger;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared;
using MyHome.Modules.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Telemetry, health checks, service discovery and HTTP resilience.
builder.AddServiceDefaults();

builder.Services.AddSharedModule();
builder.Services.AddLedgerModule();
builder.Services.AddOpenApi();

// Enums as text: numbers make payloads unreadable and shift meaning if a value is ever inserted
// in the middle of an enum.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 9457 problem details across the whole API, so clients never have to guess what went wrong
// from the shape of the body.
builder.Services.AddProblemDetails();

// Order is execution order. The disconnect check is first because it is the cheapest and the
// most frequent in development.
builder.Services.AddExceptionHandler<ClientDisconnectedExceptionHandler>();

// Validation failures become a 400 with the errors per field, once, for every endpoint.
builder.Services.AddExceptionHandler<ValidationFailedExceptionHandler>();

// The frontend runs on a different origin in development. In production both are served from the
// same one, so this only gets enabled below.
const string DevelopmentCorsPolicy = "development-spa";
builder.Services.AddCors(options => options.AddPolicy(
    DevelopmentCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

if (builder.Environment.IsDevelopment())
{
    // Until there is an identity provider, every request is attributed to the working household.
    // Development only: shipping without a real provider fails at startup rather than serving
    // data to anyone who asks.
    builder.Services.AddDevelopmentHouseholdResolver();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Schema before anything else, in every environment. Each module owns its own migration history,
// so a database several migrations behind receives exactly the ones it is missing — which is what
// lets dev and production be updated at different times without anyone tracking their state.
//
// Shared goes first: it owns the household every other module's rows point at.
await SharedSchema.MigrateAsync(app.Services).ConfigureAwait(false);
await LedgerSchema.MigrateAsync(app.Services).ConfigureAwait(false);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevelopmentCorsPolicy);

    // Sample data, never in production. Modules.Shared creates the working household and passes
    // its identifier along, so no module needs to know how a household is found.
    var householdId = await DevelopmentSeeder.EnsureSeededAsync(app.Services)
        .ConfigureAwait(false);

    await LedgerSeeder.EnsureSeededAsync(app.Services, householdId).ConfigureAwait(false);
}

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapDefaultEndpoints();
app.MapHouseholdEndpoints();
app.MapLedgerEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// The API entry point. Declared partial so integration tests can reference it with
/// <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
