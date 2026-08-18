using Claims.Core.Features.Auditing;
using Claims.Infrastructure.Auditing;
using Claims.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Claims.Tests.Features.Auditing;

public class AuditBackgroundServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeAuditWriter _writer = new();

    private static AuditEvent Event(string id, AuditAction action = AuditAction.Created) =>
        new(AuditedEntity.Claim, id, action, new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc));

    private (AuditBackgroundService Service, AuditEventChannel Channel) Build(AuditQueueOptions? options = null)
    {
        var settings = Options.Create(options ?? new AuditQueueOptions());
        var channel = new AuditEventChannel(settings);

        var service = new AuditBackgroundService(
            channel,
            _writer,
            settings,
            NullLogger<AuditBackgroundService>.Instance);

        return (service, channel);
    }

    [Fact]
    public async Task Writes_queued_events()
    {
        var (service, channel) = Build();
        await service.StartAsync(TestContext.Current.CancellationToken);

        channel.TryWrite(Event("claim-1"));

        Assert.True(await _writer.WaitForWrittenAsync(1, Timeout));
        Assert.Equal("claim-1", _writer.Written[0].EntityId);

        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Writes_several_events_queued_together_as_one_batch()
    {
        var (service, channel) = Build(new AuditQueueOptions { BatchSize = 10 });

        for (var i = 0; i < 5; i++)
        {
            channel.TryWrite(Event($"claim-{i}"));
        }

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(await _writer.WaitForWrittenAsync(5, Timeout));
        Assert.Single(_writer.Batches);

        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Honours_the_configured_batch_size()
    {
        var (service, channel) = Build(new AuditQueueOptions { BatchSize = 2 });

        for (var i = 0; i < 4; i++)
        {
            channel.TryWrite(Event($"claim-{i}"));
        }

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(await _writer.WaitForWrittenAsync(4, Timeout));
        Assert.All(_writer.Batches, batch => Assert.True(batch.Count <= 2));

        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Flushes_events_still_queued_at_shutdown()
    {
        var (service, channel) = Build();
        var releaseFirstWrite = new TaskCompletionSource();
        var firstWriteStarted = new TaskCompletionSource();

        _writer.BeforeWrite = async _ =>
        {
            if (_writer.Batches.Count == 0)
            {
                firstWriteStarted.TrySetResult();
                await releaseFirstWrite.Task;
            }
        };

        await service.StartAsync(TestContext.Current.CancellationToken);

        channel.TryWrite(Event("first"));
        await firstWriteStarted.Task;
        channel.TryWrite(Event("second"));

        var stop = service.StopAsync(CancellationToken.None);
        releaseFirstWrite.SetResult();
        await stop;

        Assert.Contains(_writer.Written, e => e.EntityId == "second");
    }

    [Fact]
    public async Task Keeps_running_after_a_failed_write()
    {
        var (service, channel) = Build(new AuditQueueOptions { BatchSize = 1 });
        var failed = false;

        _writer.BeforeWrite = _ =>
        {
            if (failed)
            {
                return Task.CompletedTask;
            }

            failed = true;
            throw new InvalidOperationException("audit database unavailable");
        };

        await service.StartAsync(TestContext.Current.CancellationToken);

        channel.TryWrite(Event("doomed"));
        channel.TryWrite(Event("survivor"));

        Assert.True(await _writer.WaitForWrittenAsync(1, Timeout));
        Assert.Contains(_writer.Written, e => e.EntityId == "survivor");

        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stops_cleanly_when_nothing_was_ever_queued()
    {
        var (service, _) = Build();

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_writer.Written);
    }

    [Fact]
    public async Task Rejects_events_recorded_after_shutdown()
    {
        var (service, channel) = Build();

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(channel.TryWrite(Event("too-late")));
    }
}