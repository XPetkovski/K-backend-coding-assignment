using Claims.Core.Features.Auditing;

namespace Claims.Infrastructure.Auditing;

internal static class AuditRecord
{
    public static object From(AuditEvent auditEvent) => auditEvent.Entity switch
    {
        AuditedEntity.Claim => new ClaimAudit
        {
            ClaimId = auditEvent.EntityId,
            Created = auditEvent.OccurredAtUtc,
            HttpRequestType = ToHttpRequestType(auditEvent.Action)
        },
        AuditedEntity.Cover => new CoverAudit
        {
            CoverId = auditEvent.EntityId,
            Created = auditEvent.OccurredAtUtc,
            HttpRequestType = ToHttpRequestType(auditEvent.Action)
        },
        _ => throw new ArgumentOutOfRangeException(nameof(auditEvent))
    };

    private static string ToHttpRequestType(AuditAction action) => action switch
    {
        AuditAction.Created => "POST",
        AuditAction.Deleted => "DELETE",
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
}