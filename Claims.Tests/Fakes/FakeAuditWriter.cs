using Claims.Core.Features.Auditing;
using Claims.Infrastructure.Auditing;

namespace Claims.Tests.Fakes;

public sealed class FakeAuditWriter : IAuditWriter
{
    private readonly Lock _gate = new();
    private readonly List<IReadOnlyList<AuditEvent>> _batches = [];

    public Func<IReadOnlyList<AuditEvent>, Task>? BeforeWrite { get; set; }

    public IReadOnlyList<IReadOnlyList<AuditEvent>> Batches
    {
        get
        {
            lock (_gate)
            {
                return _batches.ToList();
            }
        }
    }

    public IReadOnlyList<AuditEvent> Written => Batches.SelectMany(batch => batch).ToList();

    public async Task WriteAsync(IReadOnlyList<AuditEvent> auditEvents, CancellationToken cancellationToken)
    {
        if (BeforeWrite is not null)
        {
            await BeforeWrite(auditEvents);
        }

        lock (_gate)
        {
            _batches.Add(auditEvents);
        }
    }

    public async Task<bool> WaitForWrittenAsync(int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (Written.Count >= count)
            {
                return true;
            }

            await Task.Delay(10);
        }

        return Written.Count >= count;
    }
}