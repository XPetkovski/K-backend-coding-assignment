using Claims.Infrastructure.Auditing;
using Claims.Infrastructure.Mongo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Claims.Hosting;

internal sealed class PersistenceHealthCheck : IHealthCheck
{
    private readonly AuditContext _auditContext;
    private readonly ClaimsContext _claimsContext;

    public PersistenceHealthCheck(AuditContext auditContext, ClaimsContext claimsContext)
    {
        _auditContext = auditContext;
        _claimsContext = claimsContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        await ProbeAsync("audit database", _auditContext, failures, cancellationToken);
        await ProbeAsync("claims database", _claimsContext, failures, cancellationToken);

        return failures.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(string.Join("; ", failures));
    }

    private static async Task ProbeAsync(
        string name,
        DbContext context,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                failures.Add($"{name} unreachable");
            }
        }
        catch (Exception exception)
        {
            failures.Add($"{name}: {exception.Message}");
        }
    }
}