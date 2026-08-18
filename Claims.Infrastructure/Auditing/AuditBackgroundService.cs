using Claims.Core.Features.Auditing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Auditing;

public class AuditBackgroundService : BackgroundService
{
    private readonly AuditEventChannel _channel;
    private readonly IAuditWriter _writer;
    private readonly AuditQueueOptions _options;
    private readonly ILogger<AuditBackgroundService> _logger;

    public AuditBackgroundService(
        AuditEventChannel channel,
        IAuditWriter writer,
        IOptions<AuditQueueOptions> options,
        ILogger<AuditBackgroundService> logger)
    {
        _channel = channel;
        _writer = writer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                await WriteNextBatchAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        { }

        await DrainAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Complete();

        await base.StopAsync(cancellationToken);
    }
    
    private async Task DrainAsync()
    {
        using var timeout = new CancellationTokenSource(_options.ShutdownDrainTimeout);

        while (_channel.Reader.TryPeek(out _) && !timeout.IsCancellationRequested)
        {
            if (!await WriteNextBatchAsync(timeout.Token))
            {
                break;
            }
        }
    }

    private async Task<bool> WriteNextBatchAsync(CancellationToken cancellationToken)
    {
        var batch = ReadBatch();
        if (batch.Count == 0)
        {
            return true;
        }

        try
        {
            await _writer.WriteAsync(batch, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Audit write cancelled; {Count} event(s) were not persisted.", batch.Count);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to persist {Count} audit event(s).", batch.Count);
            return true;
        }
    }

    private List<AuditEvent> ReadBatch()
    {
        var batch = new List<AuditEvent>(_options.BatchSize);

        while (batch.Count < _options.BatchSize && _channel.Reader.TryRead(out var auditEvent))
        {
            batch.Add(auditEvent);
        }

        return batch;
    }
}