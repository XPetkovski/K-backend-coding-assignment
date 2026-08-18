using Claims.Core.Features.Auditing;

namespace Claims.Tests.Fakes;

public sealed class RecordingAuditTrail : IAuditTrail
{
    public List<AuditEvent> Events { get; } = [];

    public void Record(AuditEvent auditEvent) => Events.Add(auditEvent);
}