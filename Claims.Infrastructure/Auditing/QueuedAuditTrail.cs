using Claims.Core.Features.Auditing;
using Microsoft.Extensions.Logging;

namespace Claims.Infrastructure.Auditing;

public class QueuedAuditTrail : IAuditTrail
{
    private readonly AuditEventChannel _channel;
    private readonly ILogger<QueuedAuditTrail> _logger;

    public QueuedAuditTrail(AuditEventChannel channel, ILogger<QueuedAuditTrail> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    public void Record(AuditEvent auditEvent)
    {
        if (_channel.TryWrite(auditEvent))
        {
            return;
        }

        _logger.LogWarning(
            "Audit queue is full; dropped {Action} event for {Entity} {EntityId}.",
            auditEvent.Action,
            auditEvent.Entity,
            auditEvent.EntityId);
    }
}