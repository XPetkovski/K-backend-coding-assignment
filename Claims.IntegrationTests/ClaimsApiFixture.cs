using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Xunit;

namespace Claims.IntegrationTests;

public sealed class ClaimsApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            : new MsSqlBuilder())
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:latest")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();
        await _mongo.StartAsync();

        _ = Services.GetRequiredService<IServiceScopeFactory>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Persistence:UseTestContainers", "false");
        builder.UseSetting("Persistence:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Persistence:Mongo:ConnectionString", _mongo.GetConnectionString());
        builder.UseSetting("Persistence:Mongo:DatabaseName", $"claims-tests-{Guid.NewGuid():N}");
        builder.UseSetting("Persistence:AuditDbConnectionString", _sql.GetConnectionString());
        builder.UseSetting("Auditing:BatchSize", "10");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _mongo.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ClaimsApiCollection))]
public class ClaimsApiCollection : ICollectionFixture<ClaimsApiFixture>;