namespace Claims.Core.Features.Auditing;

public enum AuditedEntity
{
    Claim,
    Cover
}

public enum AuditAction
{
    Created,
    Deleted
}

public record AuditEvent(AuditedEntity Entity, string EntityId, AuditAction Action, DateTime OccurredAtUtc);

public interface IAuditTrail
{
    void Record(AuditEvent auditEvent);
}