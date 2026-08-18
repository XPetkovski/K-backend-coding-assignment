using System.Threading.Channels;
using Claims.Core.Features.Auditing;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Auditing;

public sealed class AuditEventChannel
{
    private readonly Channel<AuditEvent> _channel;

    public AuditEventChannel(IOptions<AuditQueueOptions> options)
    {
        _channel = Channel.CreateBounded<AuditEvent>(
            new BoundedChannelOptions(options.Value.Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public ChannelReader<AuditEvent> Reader => _channel.Reader;

    public bool TryWrite(AuditEvent auditEvent) => _channel.Writer.TryWrite(auditEvent);

    public void Complete() => _channel.Writer.TryComplete();
}