using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const int PostgresPort = 5432;
const int PgWebPort = 8081;
const int WebPort = 5173;

// ConnectionStrings:myhomedb, set via `dotnet user-secrets` on this project, switches the whole
// app to a remote database (e.g. Supabase) instead of the local container. Nobody without that
// secret is affected: they still get the container, untouched, on plain `dotnet run`.
IResourceBuilder<IResourceWithConnectionString> database =
    builder.Configuration.GetConnectionString("myhomedb") is not null
        ? builder.AddConnectionString("myhomedb")
        : LocalPostgresDatabase(builder);

static IResourceBuilder<PostgresDatabaseResource> LocalPostgresDatabase(
    IDistributedApplicationBuilder builder)
{
    // WithDataVolume keeps the data between restarts. Without it every restart wipes the database
    // and the test month has to be typed in again.
    return builder.AddPostgres("postgres")
        .WithDataVolume("myhome-pgdata")
        .WithHostPort(PostgresPort)
        .WithEndpointProxySupport(false)
        .WithPgWeb(pgweb => pgweb.WithHostPort(PgWebPort).WithEndpointProxySupport(false))
        .AddDatabase("myhomedb");
}

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

// The frontend is orchestrated here rather than added to the solution file.
var web = builder.AddViteApp("web", "../../../apps/web")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("http"))
    .WithEndpoint("http", endpoint => endpoint.Port = WebPort)
    .WithNpm(install: false)
    .WithExternalHttpEndpoints()

    // Without a health check the resource counts as ready the moment the process starts, which is
    // before Vite has finished its first build. The browser would then open on a refused
    // connection.
    .WithHttpHealthCheck("/")
    .WithEndpointProxySupport(false);

// Open the app once Vite is actually answering. The dashboard still opens on its own; this is the
// screen you actually want to look at.
var alreadyOpened = 0;

builder.Eventing.Subscribe<ResourceReadyEvent>(web.Resource, (@event, cancellationToken) =>
{
    // The event fires again whenever the resource is restarted from the dashboard, and a new tab
    // on every restart gets old fast.
    if (Interlocked.Exchange(ref alreadyOpened, 1) != 0)
    {
        return Task.CompletedTask;
    }

    try
    {
        // UseShellExecute hands the URL to the OS, which is what makes this work with whatever
        // the default browser happens to be.
        Process.Start(new ProcessStartInfo(web.GetEndpoint("http").Url) { UseShellExecute = true });
    }
    catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
        or InvalidOperationException
        or PlatformNotSupportedException)
    {
        // Not being able to open a browser is not a reason to fail the run. The dashboard still
        // lists the URL.
    }

    return Task.CompletedTask;
});

builder.Build().Run();
