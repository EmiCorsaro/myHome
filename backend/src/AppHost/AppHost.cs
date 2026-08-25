using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const int PostgresPort = 5432;
const int PgWebPort = 8081;
const int WebPort = 5173;

IResourceBuilder<IResourceWithConnectionString> database =
    builder.Configuration.GetConnectionString("myhomedb") is not null
        ? builder.AddConnectionString("myhomedb")
        : LocalPostgresDatabase(builder);

static IResourceBuilder<PostgresDatabaseResource> LocalPostgresDatabase(
    IDistributedApplicationBuilder builder)
{
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

var web = builder.AddViteApp("web", "../../../apps/web")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("http"))
    .WithEndpoint("http", endpoint => endpoint.Port = WebPort)
    .WithNpm(install: false)
    .WithExternalHttpEndpoints()

    .WithHttpHealthCheck("/")
    .WithEndpointProxySupport(false);

var alreadyOpened = 0;

builder.Eventing.Subscribe<ResourceReadyEvent>(web.Resource, (@event, cancellationToken) =>
{
    if (Interlocked.Exchange(ref alreadyOpened, 1) != 0)
    {
        return Task.CompletedTask;
    }

    try
    {
        Process.Start(new ProcessStartInfo(web.GetEndpoint("http").Url) { UseShellExecute = true });
    }
    catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
        or InvalidOperationException
        or PlatformNotSupportedException)
    {
    }

    return Task.CompletedTask;
});

builder.Build().Run();
