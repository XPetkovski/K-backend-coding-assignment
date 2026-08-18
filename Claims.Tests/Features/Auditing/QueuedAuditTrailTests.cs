using Claims.Core.Features.Auditing;
using Claims.Infrastructure.Auditing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Claims.Tests.Features.Auditing;

public class QueuedAuditTrailTests
{
    private static AuditEvent Event(string id = "claim-1") =>
        new(AuditedEntity.Claim, id, AuditAction.Created, new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc));

    private static (QueuedAuditTrail Trail, AuditEventChannel Channel) Build(int capacity)
    {
        var channel = new AuditEventChannel(Options.Create(new AuditQueueOptions { Capacity = capacity }));

        return (new QueuedAuditTrail(channel, NullLogger<QueuedAuditTrail>.Instance), channel);
    }

    [Fact]
    public void Queues_the_event_without_writing_it()
    {
        var (trail, channel) = Build(capacity: 10);

        trail.Record(Event());

        Assert.True(channel.Reader.TryRead(out var queued));
        Assert.Equal("claim-1", queued!.EntityId);
    }

    [Fact]
    public void Preserves_the_order_events_were_recorded_in()
    {
        var (trail, channel) = Build(capacity: 10);

        trail.Record(Event("first"));
        trail.Record(Event("second"));

        channel.Reader.TryRead(out var first);
        channel.Reader.TryRead(out var second);

        Assert.Equal("first", first!.EntityId);
        Assert.Equal("second", second!.EntityId);
    }

    [Fact]
    public void Drops_events_without_throwing_once_the_queue_is_full()
    {
        var (trail, _) = Build(capacity: 2);

        trail.Record(Event("first"));
        trail.Record(Event("second"));

        trail.Record(Event("overflow"));
    }

    [Fact]
    public void Returns_promptly_when_the_queue_is_full()
    {
        var (trail, _) = Build(capacity: 1);
        trail.Record(Event("first"));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        trail.Record(Event("overflow"));
        stopwatch.Stop();

        Assert.True(
            stopwatch.ElapsedMilliseconds < 500,
            $"Record blocked for {stopwatch.ElapsedMilliseconds}ms on a full queue.");
    }
}