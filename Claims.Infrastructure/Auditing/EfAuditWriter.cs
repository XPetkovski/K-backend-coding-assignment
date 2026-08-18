using Claims.Core.Features.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Infrastructure.Auditing;

public interface IAuditWriter
{
    Task WriteAsync(IReadOnlyList<AuditEvent> auditEvents, CancellationToken cancellationToken);
}

public class EfAuditWriter : IAuditWriter
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfAuditWriter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task WriteAsync(IReadOnlyList<AuditEvent> auditEvents, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditContext>();

        foreach (var auditEvent in auditEvents)
        {
            context.Add(AuditRecord.From(auditEvent));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}