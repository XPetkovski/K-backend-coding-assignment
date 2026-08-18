using System.Runtime.InteropServices;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace Claims.Hosting;

// Throwaway databases for local development only, enabled by Persistence:UseTestContainers.
// Requires a running Docker daemon.
internal sealed class LocalContainers : IAsyncDisposable
{
    private readonly MsSqlContainer _sql;
    private readonly MongoDbContainer _mongo;

    private LocalContainers(MsSqlContainer sql, MongoDbContainer mongo)
    {
        _sql = sql;
        _mongo = mongo;
    }

    public string SqlConnectionString => _sql.GetConnectionString();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public static async Task<LocalContainers> StartAsync(CancellationToken cancellationToken = default)
    {
        var sql = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                : new MsSqlBuilder())
            .Build();

        var mongo = new MongoDbBuilder()
            .WithImage("mongo:latest")
            .Build();

        await sql.StartAsync(cancellationToken);
        await mongo.StartAsync(cancellationToken);

        return new LocalContainers(sql, mongo);
    }

    public async ValueTask DisposeAsync()
    {
        await _mongo.DisposeAsync();
        await _sql.DisposeAsync();
    }
}